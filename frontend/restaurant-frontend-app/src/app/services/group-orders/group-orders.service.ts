import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, switchMap } from 'rxjs';
import { SKIP_ERROR_TOAST } from '../../auth/skip-error-toast.context';
import { GroupSessionsResponse } from '../../models/group-orders/group-sessions-response.model';
import { GroupStatusResponse } from '../../models/group-orders/group-status.model';
import { OwnerSessionResponse } from '../../models/group-orders/owner-session.model';
import { SubmitGroupPreOrderRequest, SubmittedDish } from '../../models/group-orders/submit-group-pre-order.model';
import { GuestInvite } from '../../models/reservations/guest-invite.model';
import { API_BASE_URL } from '../../shared/config/api-base-url.token';
import { GuestInviteService } from '../reservation/guest-invite.service';

/** Тихий запит: помилку не показуємо toast-ом (фонова синхронізація). */
export interface GroupRequestOptions {
  silent?: boolean;
}

/**
 * HTTP-клієнт екрана Owner групового pre-order.
 * Ендпоінти — з homework-backend.md; у dev для demo-reservation відповідає мок-інтерсептор.
 */
@Injectable({ providedIn: 'root' })
export class GroupOrdersService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly guestInvites = inject(GuestInviteService);

  /** reservationId → invite-token → invite (у ньому GroupReservationId і дані для шапки). */
  getGroupInvite$(reservationId: string): Observable<GuestInvite> {
    return this.guestInvites
      .getInviteToken$(reservationId)
      .pipe(switchMap(({ token }) => this.guestInvites.getInvite$(token)));
  }

  getSessions$(groupReservationId: string, options: GroupRequestOptions = {}): Observable<GroupSessionsResponse> {
    return this.http.get<GroupSessionsResponse>(`${this.groupUrl(groupReservationId)}/sessions`, {
      context: this.context(options),
    });
  }

  getOwnerSessionId$(reservationId: string): Observable<string> {
    return this.http
      .get<OwnerSessionResponse>(`${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}/guest-session-id`)
      .pipe(map((response) => response.guestSessionId));
  }

  getStatus$(groupReservationId: string, options: GroupRequestOptions = {}): Observable<GroupStatusResponse> {
    return this.http.get<GroupStatusResponse>(`${this.groupUrl(groupReservationId)}/status`, {
      context: this.context(options),
    });
  }

  submit$(groupReservationId: string, reservationId: string): Observable<SubmittedDish[]> {
    const body: SubmitGroupPreOrderRequest = { reservationId };
    return this.http.post<SubmittedDish[]>(
      `${this.baseUrl}/group-reservations/${encodeURIComponent(groupReservationId)}/submit`,
      body,
    );
  }

  removeSession$(groupReservationId: string, guestSessionId: string): Observable<GroupSessionsResponse> {
    return this.http.delete<GroupSessionsResponse>(
      `${this.groupUrl(groupReservationId)}/sessions/${encodeURIComponent(guestSessionId)}`,
    );
  }

  private groupUrl(groupReservationId: string): string {
    return `${this.baseUrl}/guest/groups/${encodeURIComponent(groupReservationId)}`;
  }

  private context(options: GroupRequestOptions): HttpContext {
    return new HttpContext().set(SKIP_ERROR_TOAST, options.silent === true);
  }
}
