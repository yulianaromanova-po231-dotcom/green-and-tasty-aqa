import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  EMPTY,
  Observable,
  Subscription,
  catchError,
  defer,
  exhaustMap,
  finalize,
  forkJoin,
  interval,
  map,
  of,
  switchMap,
  tap,
} from 'rxjs';
import { ParticipantCardComponent } from '../../components/group-orders/participant-card/participant-card.component';
import { GroupSessionsResponse } from '../../models/group-orders/group-sessions-response.model';
import { GroupStatusResponse } from '../../models/group-orders/group-status.model';
import { Participant } from '../../models/group-orders/participant.model';
import { GuestInvite } from '../../models/reservations/guest-invite.model';
import { GroupOrdersService } from '../../services/group-orders/group-orders.service';
import { ToastService } from '../../services/toast/toast.service';
import { ModalComponent } from '../../shared/custom-components/modal/modal.component';
import { consolidatePreOrder } from '../../shared/helpers/consolidate-pre-order';
import { GroupOrderLoadState } from './models';

/** Як часто підтягуємо свіжі кошики, поки група Open. */
export const GROUP_ORDER_SYNC_MS = 15_000;

const STATUS_BADGES: Record<GroupStatusResponse['status'], string> = {
  Open: 'Collecting pre-orders',
  Submitted: 'Sent to Restaurant',
  Closed: 'Closed',
};

/**
 * Екран Owner групового pre-order: кошики всіх учасників, консолідоване замовлення,
 * Submit в ресторан, Remove Guest і фонова синхронізація кожні 15 с.
 */
@Component({
  selector: 'app-group-order-page',
  imports: [ParticipantCardComponent, ModalComponent, CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './group-order-page.component.html',
  styleUrl: './group-order-page.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GroupOrderPageComponent {
  private readonly groupOrders = inject(GroupOrdersService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly reservationId = inject(ActivatedRoute).snapshot.paramMap.get('reservationId') ?? '';

  // ---- стан ----
  readonly loadState = signal<GroupOrderLoadState>('loading');
  readonly invite = signal<GuestInvite | null>(null);
  readonly participants = signal<Participant[]>([]);
  readonly ownerSessionId = signal<string | null>(null);
  readonly groupStatus = signal<GroupStatusResponse | null>(null);
  readonly lastUpdated = signal<Date | null>(null);
  readonly refreshFailed = signal(false);
  readonly refreshing = signal(false);

  readonly submitDialogOpen = signal(false);
  readonly submitting = signal(false);

  readonly removeTarget = signal<Participant | null>(null);
  readonly removing = signal(false);

  // ---- похідні значення ----
  /** Замовлення для ресторану: лише підтверджені кошики, однакові страви — одним рядком. */
  readonly consolidated = computed(() => consolidatePreOrder(this.participants()));
  /** Grand Total = сума консолідованого замовлення, а не `total` з API. */
  readonly grandTotal = computed(() => this.consolidated().total);

  readonly status = computed(() => this.groupStatus()?.status ?? 'Open');
  readonly isOpen = computed(() => this.status() === 'Open');
  readonly isSubmitted = computed(() => this.status() === 'Submitted');
  readonly badgeText = computed(() => STATUS_BADGES[this.status()]);

  readonly pendingCount = computed(() => this.participants().filter((person) => !person.isSubmitted).length);
  readonly pendingMessage = computed(() => {
    const count = this.pendingCount();
    return count === 1
      ? '1 participant hasn’t submitted their cart yet — their dishes won’t be included in the order'
      : `${count} participants haven’t submitted their cart yet — their dishes won’t be included in the order`;
  });

  readonly hasAnyDishes = computed(() => this.participants().some((person) => person.items.length > 0));
  readonly guestCountLabel = computed(() => {
    const max = this.invite()?.MaxGuestCount;
    const current = this.participants().length;
    return max ? `${current} of ${max} guests` : `${current} guests`;
  });

  readonly canSubmit = computed(
    () => this.isOpen() && this.consolidated().items.length > 0 && !this.submitting(),
  );

  private groupReservationId = '';
  private syncSubscription: Subscription | null = null;
  private syncInFlight = false;

  constructor() {
    this.destroyRef.onDestroy(() => this.stopSync());
    this.load();
  }

  // ---- завантаження ----
  load(): void {
    this.loadState.set('loading');

    this.groupOrders
      .getGroupInvite$(this.reservationId)
      .pipe(
        switchMap((invite) => {
          this.groupReservationId = invite.GroupReservationId;
          // id власної сесії — першим: бекенд створює owner-сесію при першому запиті,
          // і тоді вона одразу потрапляє у список учасників.
          return this.groupOrders.getOwnerSessionId$(this.reservationId).pipe(
            catchError(() => of(null)),
            switchMap((ownerSessionId) =>
              forkJoin({
                sessions: this.groupOrders.getSessions$(invite.GroupReservationId),
                status: this.groupOrders.getStatus$(invite.GroupReservationId),
              }).pipe(map((data) => ({ invite, ownerSessionId, ...data }))),
            ),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: ({ invite, ownerSessionId, sessions, status }) => {
          this.invite.set(invite);
          this.ownerSessionId.set(ownerSessionId);
          this.applySnapshot(sessions, status);
          this.loadState.set('ready');
          this.startSync();
        },
        error: (error: unknown) => {
          this.loadState.set(this.loadErrorState(error));
        },
      });
  }

  /**
   * 404 → немає групи; 400 GroupReservationSubmitted → бекенд уже не видає invite-token,
   * бо замовлення відправлене в ресторан (сторінку після submit перезавантажили).
   */
  private loadErrorState(error: unknown): GroupOrderLoadState {
    if (!(error instanceof HttpErrorResponse)) return 'error';
    if (error.status === 404) return 'not-found';
    const body = error.error as { errorCode?: string; ErrorCode?: string } | null;
    const code = body?.errorCode ?? body?.ErrorCode;
    if (error.status === 400 && code === 'GroupReservationSubmitted') return 'already-submitted';
    return 'error';
  }

  /** Кнопка Refresh: синхронізуватись зараз (тихо, без лоадера на весь екран). */
  refresh(): void {
    this.syncOnce$().pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
  }

  // ---- Submit ----
  openSubmitDialog(): void {
    if (this.canSubmit()) this.submitDialogOpen.set(true);
  }

  closeSubmitDialog(): void {
    if (!this.submitting()) this.submitDialogOpen.set(false);
  }

  confirmSubmit(): void {
    if (this.submitting() || !this.canSubmit()) return; // подвійний клік не відправляє двічі
    this.submitting.set(true);

    this.groupOrders
      .submit$(this.groupReservationId, this.reservationId)
      .pipe(
        finalize(() => this.submitting.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: () => {
          const current = this.groupStatus();
          this.groupStatus.set(
            current ? { ...current, status: 'Submitted' } : { currentParticipants: this.participants().length, maxCapacity: 0, status: 'Submitted' },
          );
          this.submitDialogOpen.set(false);
          this.stopSync();
          this.toast.success('Pre-order sent to the restaurant');
        },
        // Toast з текстом від API вже показує tokenInterceptor — тут лише повертаємо екран у робочий стан.
        error: () => this.submitDialogOpen.set(false),
      });
  }

  // ---- Remove Guest ----
  askRemove(participant: Participant): void {
    if (this.isOpen()) this.removeTarget.set(participant);
  }

  closeRemoveDialog(): void {
    if (!this.removing()) this.removeTarget.set(null);
  }

  confirmRemove(): void {
    const target = this.removeTarget();
    if (!target || this.removing()) return;
    this.removing.set(true);

    this.groupOrders
      .removeSession$(this.groupReservationId, target.id)
      .pipe(
        finalize(() => this.removing.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (sessions) => {
          this.participants.set(sessions.participants);
          this.lastUpdated.set(new Date());
          this.removeTarget.set(null);
          this.toast.success(`${target.name} was removed from the group`);
        },
        error: () => this.removeTarget.set(null),
      });
  }

  isMe(participant: Participant): boolean {
    return participant.id === this.ownerSessionId();
  }

  // ---- синхронізація ----
  private applySnapshot(sessions: GroupSessionsResponse, status: GroupStatusResponse): void {
    this.participants.set(sessions.participants);
    this.groupStatus.set(status);
    this.lastUpdated.set(new Date());
    this.refreshFailed.set(false);
  }

  /** Один таймер на сторінку: повторне завантаження (Try again) другого не створює. */
  private startSync(): void {
    if (this.syncSubscription || !this.isOpen()) return;
    this.syncSubscription = interval(GROUP_ORDER_SYNC_MS)
      .pipe(
        exhaustMap(() => this.syncOnce$()), // попередній запит ще не відповів — новий не стартує
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();
  }

  private stopSync(): void {
    this.syncSubscription?.unsubscribe();
    this.syncSubscription = null;
  }

  private syncOnce$(): Observable<void> {
    return defer(() => {
      if (this.syncInFlight || !this.groupReservationId) return EMPTY;
      this.syncInFlight = true;
      this.refreshing.set(true);

      return forkJoin({
        sessions: this.groupOrders.getSessions$(this.groupReservationId, { silent: true }),
        status: this.groupOrders.getStatus$(this.groupReservationId, { silent: true }),
      }).pipe(
        tap(({ sessions, status }) => {
          this.applySnapshot(sessions, status);
          if (status.status !== 'Open') this.stopSync();
        }),
        map(() => undefined),
        // Лишаємо останні дані й пишемо «Couldn’t refresh…» — без toast на кожну спробу.
        catchError(() => {
          this.refreshFailed.set(true);
          return EMPTY;
        }),
        finalize(() => {
          this.syncInFlight = false;
          this.refreshing.set(false);
        }),
      );
    });
  }
}
