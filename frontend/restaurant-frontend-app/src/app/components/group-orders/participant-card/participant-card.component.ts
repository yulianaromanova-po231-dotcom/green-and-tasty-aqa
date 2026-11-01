import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { Participant } from '@app/models/group-orders/participant.model';

/**
 * Картка одного учасника групового pre-order: заголовок, статус кошика текстом,
 * страви (фото або перша літера), кількість, ціна і Subtotal.
 */
@Component({
  selector: 'app-participant-card',
  imports: [CurrencyPipe],
  templateUrl: './participant-card.component.html',
  styleUrl: './participant-card.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class ParticipantCardComponent {
  /** Учасник, якого показує картка. */
  readonly participant = input.required<Participant>();
  /** Власна картка Owner — підписується «My Pre-Order». */
  readonly isMe = input(false);
  /** Чи показувати «Remove Guest» (лише для гостей, поки група відкрита). */
  readonly canRemove = input(false);

  readonly removeClick = output<Participant>();

  readonly title = computed(() => {
    const person = this.participant();
    if (this.isMe()) return 'My Pre-Order';
    return person.type === 'Owner' ? `Owner: ${person.name}` : `Guest: ${person.name}`;
  });

  readonly showRemove = computed(() => this.canRemove() && this.participant().type === 'Guest');

  readonly removeLabel = computed(() => `Remove ${this.participant().name}`);

  onRemove(): void {
    this.removeClick.emit(this.participant());
  }
}
