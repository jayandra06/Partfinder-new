import { LicenseController } from './license.controller';

describe('LicenseController', () => {
  it('verifies the org code through the organizations service', async () => {
    const verifyLicense = jest.fn().mockResolvedValue({ valid: true, orgName: 'Acme' });
    const ctrl = new LicenseController({ verifyLicense } as any);

    await expect(ctrl.verify({ orgCode: 'ACME01' } as any)).resolves.toEqual({ valid: true, orgName: 'Acme' });
    expect(verifyLicense).toHaveBeenCalledWith('ACME01');
  });

  it('propagates verification failures', async () => {
    const verifyLicense = jest.fn().mockRejectedValue(new Error('Invalid license'));
    const ctrl = new LicenseController({ verifyLicense } as any);

    await expect(ctrl.verify({ orgCode: 'BAD' } as any)).rejects.toThrow('Invalid license');
  });
});
