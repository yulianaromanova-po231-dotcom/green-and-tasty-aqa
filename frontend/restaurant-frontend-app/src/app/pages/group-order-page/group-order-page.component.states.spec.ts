// Додаткові стани завантаження сторінки Owner (поверх тестів домашки).
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { throwError } from 'rxjs';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { GroupOrderPageComponent } from './group-order-page.component';

describe('Сторінка Owner · стани помилок завантаження', () => {
  let service: jasmine.SpyObj<GroupOrdersService>;

  function createWithError(error: unknown) {
    service = jasmine.createSpyObj<GroupOrdersService>('GroupOrdersService', [
      'getGroupInvite$', 'getSessions$', 'getOwnerSessionId$', 'getStatus$', 'submit$',
    ]);
    service.getGroupInvite$.and.returnValue(throwError(() => error));
    TestBed.configureTestingModule({
      imports: [GroupOrderPageComponent],
      providers: [
        provideRouter([]),
        { provide: GroupOrdersService, useValue: service },
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['success', 'error', 'info']) },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ reservationId: 'res-1' }) } } },
      ],
    });
    const fixture = TestBed.createComponent(GroupOrderPageComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('400 GroupReservationSubmitted (camelCase) → «Pre-order already sent to the restaurant»', () => {
    const element = createWithError(
      new HttpErrorResponse({ status: 400, error: { message: 'Group reservation was submitted.', errorCode: 'GroupReservationSubmitted' } }),
    );
    expect(element.textContent).toContain('Pre-order already sent to the restaurant');
    expect(element.textContent).not.toContain('Try again');
  });

  it('400 GroupReservationSubmitted (PascalCase) теж розпізнається', () => {
    const element = createWithError(
      new HttpErrorResponse({ status: 400, error: { Message: 'Group reservation was submitted.', ErrorCode: 'GroupReservationSubmitted' } }),
    );
    expect(element.textContent).toContain('Pre-order already sent to the restaurant');
  });

  it('інша 400 → загальна помилка з «Try again»', () => {
    const element = createWithError(new HttpErrorResponse({ status: 400, error: { errorCode: 'Unknown' } }));
    expect(element.textContent).toContain('Try again');
  });

  it('404 → «Group reservation not found»', () => {
    const element = createWithError(new HttpErrorResponse({ status: 404 }));
    expect(element.textContent).toContain('Group reservation not found');
  });
});
