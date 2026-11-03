import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ToastService } from '../services/toast/toast.service';
import { AuthenticationService } from './services/authentication.service';
import { SKIP_ERROR_TOAST } from './skip-error-toast.context';
import { tokenInterceptor } from './token.interceptor';

const URL = 'https://test.api/reservations';

describe('tokenInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let toastService: jasmine.SpyObj<ToastService>;

  beforeEach(() => {
    toastService = jasmine.createSpyObj<ToastService>('ToastService', ['error']);
    const authService = jasmine.createSpyObj<AuthenticationService>('AuthenticationService', [
      'getTokens',
    ]);
    authService.getTokens.and.returnValue(null);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([tokenInterceptor])),
        provideHttpClientTesting(),
        { provide: ToastService, useValue: toastService },
        { provide: AuthenticationService, useValue: authService },
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('shows the server message in a toast for a 400 response', () => {
    http.post(URL, {}).subscribe({ error: () => undefined });

    httpMock
      .expectOne(URL)
      .flush({ message: 'Overlaps' }, { status: 400, statusText: 'Bad Request' });

    expect(toastService.error).toHaveBeenCalledWith('Overlaps', 'Error');
  });

  it('does not show a toast but still rethrows when SKIP_ERROR_TOAST is set', () => {
    let receivedStatus: number | undefined;
    http
      .post(URL, {}, { context: new HttpContext().set(SKIP_ERROR_TOAST, true) })
      .subscribe({ error: (error) => (receivedStatus = error.status) });

    httpMock
      .expectOne(URL)
      .flush({ message: 'Overlaps' }, { status: 400, statusText: 'Bad Request' });

    expect(toastService.error).not.toHaveBeenCalled();
    expect(receivedStatus).toBe(400);
  });

  it('shows a toast for a failed guest endpoint by default', () => {
    const guestUrl = 'https://test.api/guest/groups/grp-1/sessions';
    http.get(guestUrl).subscribe({ error: () => undefined });

    httpMock.expectOne(guestUrl).flush({ error: 'down' }, { status: 500, statusText: 'Server Error' });

    expect(toastService.error).toHaveBeenCalledWith('Server error. Please try again later.', 'Server Error');
  });

  it('skips the toast for a silent guest endpoint request but still rethrows', () => {
    const guestUrl = 'https://test.api/guest/groups/grp-1/sessions';
    let receivedStatus: number | undefined;
    http
      .get(guestUrl, { context: new HttpContext().set(SKIP_ERROR_TOAST, true) })
      .subscribe({ error: (error) => (receivedStatus = error.status) });

    httpMock.expectOne(guestUrl).flush({ error: 'down' }, { status: 500, statusText: 'Server Error' });

    expect(toastService.error).not.toHaveBeenCalled();
    expect(receivedStatus).toBe(500);
  });

  describe('with a signed-in user', () => {
    beforeEach(() => {
      const authService = TestBed.inject(AuthenticationService) as jasmine.SpyObj<AuthenticationService>;
      authService.getTokens.and.returnValue({ idToken: 'owner-id-token' } as ReturnType<AuthenticationService['getTokens']>);
    });

    it('adds the Cognito token to the owner endpoints under /guest/groups/', () => {
      const ownerUrl = 'https://test.api/guest/groups/grp-1/sessions';
      http.get(ownerUrl).subscribe();

      const req = httpMock.expectOne(ownerUrl);
      expect(req.request.headers.get('Authorization')).toBe('Bearer owner-id-token');
      req.flush({ participants: [], total: 0 });
    });

    it('leaves guest-session endpoints untouched', () => {
      const guestUrl = 'https://test.api/guest/sessions/s-1/dishes';
      http.post(guestUrl, { dishId: 'd', quantity: 1 }).subscribe();

      const req = httpMock.expectOne(guestUrl);
      expect(req.request.headers.has('Authorization')).toBeFalse();
      req.flush({});
    });
  });
});
