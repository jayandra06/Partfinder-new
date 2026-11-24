import { useEffect, useState } from 'react'
import type { CSSProperties } from 'react'

const LOGO_LIGHT = '/partfinder-logo-light.png'
const LOGO_DARK = '/partfinder-logo-dark.png'

export type PartFinderLogoVariant = 'light' | 'dark' | 'auto'

type PartFinderLogoProps = {
  className?: string
  style?: CSSProperties
  /** Light = navy logo for light backgrounds; dark = white logo for dark UI. */
  variant?: PartFinderLogoVariant
  /** Use `alt=""` when the image is decorative next to visible text. */
  alt?: string
}

function resolveVariant(variant: PartFinderLogoVariant): 'light' | 'dark' {
  if (variant !== 'auto') {
    return variant
  }
  if (typeof document === 'undefined') {
    return 'dark'
  }
  return document.documentElement.classList.contains('light-theme') ? 'light' : 'dark'
}

export function PartFinderLogo({
  className,
  style,
  variant = 'auto',
  alt = 'PartFinder',
}: PartFinderLogoProps) {
  const [active, setActive] = useState<'light' | 'dark'>(() => resolveVariant(variant))

  useEffect(() => {
    if (variant !== 'auto') {
      setActive(variant)
      return
    }
    const root = document.documentElement
    const sync = () => setActive(root.classList.contains('light-theme') ? 'light' : 'dark')
    sync()
    const observer = new MutationObserver(sync)
    observer.observe(root, { attributes: true, attributeFilter: ['class'] })
    return () => observer.disconnect()
  }, [variant])

  return (
    <img
      className={className}
      style={style}
      src={active === 'light' ? LOGO_LIGHT : LOGO_DARK}
      alt={alt}
    />
  )
}
