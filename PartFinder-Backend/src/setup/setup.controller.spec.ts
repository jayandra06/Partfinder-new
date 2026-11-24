import { SetupController } from './setup.controller';

describe('SetupController', () => {
  const methods = [
    'status', 'provisionDefaultDatabase', 'saveCustomDatabase', 'testDatabase', 'createOrgAdmin', 'inviteOrgUser',
    'listOrgUsers', 'validateInviteLogin', 'refreshDeviceSession', 'revokeDeviceSession', 'changePassword',
    'requestPasswordReset', 'resetPasswordWithToken',
  ];

  function setup(env: Record<string, string> = {}) {
    const service: Record<string, jest.Mock> = {};
    for (const m of methods) service[m] = jest.fn().mockResolvedValue({ from: m });
    const config = { get: jest.fn((k: string) => env[k]) };
    return { controller: new SetupController(service as any, config as any), service };
  }

  it.each([
    ['status', 'status', { orgCode: '123456' }, ['123456']],
    ['provisionDefault', 'provisionDefaultDatabase', { orgCode: '123456' }, ['123456']],
    ['testDatabase', 'testDatabase', { orgCode: '123456', uri: 'mongodb://h/db' }, ['123456', 'mongodb://h/db']],
    ['listOrgUsers', 'listOrgUsers', { orgCode: '123456' }, ['123456']],
  ])('%s unwraps the DTO for SetupService.%s', async (action, method, dto, args) => {
    const { controller, service } = setup();

    await expect((controller as any)[action](dto)).resolves.toEqual({ from: method });
    expect(service[method]).toHaveBeenCalledWith(...args);
  });

  it.each([
    ['saveCustom', 'saveCustomDatabase'],
    ['createOrgAdmin', 'createOrgAdmin'],
    ['inviteUser', 'inviteOrgUser'],
    ['inviteLogin', 'validateInviteLogin'],
    ['refreshSession', 'refreshDeviceSession'],
    ['revokeSession', 'revokeDeviceSession'],
    ['changePassword', 'changePassword'],
    ['forgotPassword', 'requestPasswordReset'],
    ['resetPassword', 'resetPasswordWithToken'],
  ])('%s passes the whole DTO to SetupService.%s', async (action, method) => {
    const { controller, service } = setup();
    const dto = { orgCode: '123456', marker: action };

    await expect((controller as any)[action](dto)).resolves.toEqual({ from: method });
    expect(service[method]).toHaveBeenCalledWith(dto);
  });

  function fakeRes() {
    const res: any = { type: jest.fn(() => res), send: jest.fn(() => res) };
    return res;
  }

  it('serves the reset-password form posting to the configured public API', () => {
    const { controller } = setup({ PUBLIC_API_BASE_URL: 'https://api.example.com/' });
    const res = fakeRes();

    controller.resetPasswordForm('tok<1>', res);

    expect(res.type).toHaveBeenCalledWith('html');
    const html: string = res.send.mock.calls[0][0];
    expect(html).toContain('const apiUrl = "https://api.example.com/api/public/setup/reset-password"');
    expect(html).toContain('value="tok&lt;1&gt;"');
  });

  it('falls back to the default backend URL and an empty token', () => {
    const { controller } = setup();
    const res = fakeRes();

    controller.resetPasswordForm(undefined as any, res);

    const html: string = res.send.mock.calls[0][0];
    expect(html).toContain('https://backend.partfinder.store/api/public/setup/reset-password');
    expect(html).toContain('<input type="hidden" id="token" value="" />');
  });
});
