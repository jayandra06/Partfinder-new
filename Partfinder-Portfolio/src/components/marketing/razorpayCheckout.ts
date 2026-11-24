import { buildApiUrl, readFetchErrorMessage } from './accountSession'

export type LicenceCredentials = {
  organisationName: string
  email: string
  orgCode: string
  temporaryPassword: string
}

type CheckoutSession = {
  keyId: string
  orderId: string
  amount: number
  currency: string
  description: string
  prefill: {
    name: string
    email: string
    contact: string
  }
}

type RazorpayHandlerResponse = {
  razorpay_payment_id: string
  razorpay_order_id: string
  razorpay_signature: string
}

type RazorpayInstance = {
  open: () => void
  on: (event: string, handler: (response: { error?: { description?: string } }) => void) => void
}

type RazorpayConstructor = new (options: Record<string, unknown>) => RazorpayInstance

declare global {
  interface Window {
    Razorpay?: RazorpayConstructor
  }
}

const SCRIPT_SRC = 'https://checkout.razorpay.com/v1/checkout.js'

async function parseResponseBody(response: Response): Promise<unknown> {
  const text = await response.text()
  if (!text.trim()) return null
  try {
    return JSON.parse(text) as unknown
  } catch {
    return text
  }
}

let scriptPromise: Promise<void> | null = null

function loadRazorpayScript() {
  if (window.Razorpay) {
    return Promise.resolve()
  }
  if (scriptPromise) {
    return scriptPromise
  }
  scriptPromise = new Promise((resolve, reject) => {
    const existing = document.querySelector(`script[src="${SCRIPT_SRC}"]`)
    if (existing) {
      existing.addEventListener('load', () => resolve())
      existing.addEventListener('error', () => reject(new Error('Could not load Razorpay.')))
      return
    }
    const script = document.createElement('script')
    script.src = SCRIPT_SRC
    script.async = true
    script.onload = () => resolve()
    script.onerror = () => reject(new Error('Could not load Razorpay.'))
    document.body.appendChild(script)
  })
  return scriptPromise
}

export async function startLicencePayment(input: {
  organisationName: string
  email: string
  mobile: string
}): Promise<LicenceCredentials> {
  const response = await fetch(buildApiUrl('/public/license-purchase/checkout'), {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  })
  const data = await parseResponseBody(response)
  if (!response.ok) {
    throw new Error(
      readFetchErrorMessage(response, data, 'Could not start checkout. Is the API running with licence purchase enabled?'),
    )
  }
  const session = data as CheckoutSession & { ok?: boolean; keyId?: string; orderId?: string }
  if (!session.keyId || !session.orderId) {
    throw new Error('Invalid checkout response from server.')
  }

  await loadRazorpayScript()
  if (!window.Razorpay) {
    throw new Error('Razorpay checkout is unavailable.')
  }

  return new Promise((resolve, reject) => {
    const rzp = new window.Razorpay!({
      key: session.keyId,
      amount: session.amount,
      currency: session.currency,
      name: 'PartFinder',
      description: session.description,
      order_id: session.orderId,
      prefill: session.prefill,
      theme: { color: '#2563EB' },
      handler: async (payment: RazorpayHandlerResponse) => {
        try {
          const verified = await verifyLicencePayment(payment)
          resolve(verified)
        } catch (error) {
          reject(error instanceof Error ? error : new Error('Payment verification failed.'))
        }
      },
      modal: {
        ondismiss: () => {
          reject(new Error('Payment was cancelled.'))
        },
      },
    })
    rzp.on('payment.failed', (payload) => {
      const description = payload.error?.description ?? 'Payment failed.'
      reject(new Error(description))
    })
    rzp.open()
  })
}

async function verifyLicencePayment(payment: RazorpayHandlerResponse): Promise<LicenceCredentials> {
  const response = await fetch(buildApiUrl('/public/license-purchase/verify'), {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      razorpayOrderId: payment.razorpay_order_id,
      razorpayPaymentId: payment.razorpay_payment_id,
      razorpaySignature: payment.razorpay_signature,
    }),
  })
  const data = await parseResponseBody(response)
  if (!response.ok) {
    throw new Error(readFetchErrorMessage(response, data, 'Could not verify payment.'))
  }
  const result = data as LicenceCredentials & { ok?: boolean }
  if (!result.orgCode || !result.temporaryPassword) {
    throw new Error('Licence was not issued. Contact support with your payment reference.')
  }
  return {
    organisationName: result.organisationName,
    email: result.email,
    orgCode: result.orgCode,
    temporaryPassword: result.temporaryPassword,
  }
}
