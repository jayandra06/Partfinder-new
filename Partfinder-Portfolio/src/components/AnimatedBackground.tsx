import React, { useEffect, useRef } from 'react'

interface AnimatedBackgroundProps {
  theme: 'light' | 'dark'
}

interface Star {
  x: number
  y: number
  size: number
  alpha: number
  twinkleSpeed: number
  twinkleDir: number
}

interface CloudSegment {
  dx: number
  dy: number
  rx: number
  ry: number
}

interface Cloud {
  x: number
  y: number
  speed: number
  segments: CloudSegment[]
  opacity: number
}

interface ShootingStar {
  x: number
  y: number
  length: number
  speed: number
  angle: number
  opacity: number
  active: boolean
}

interface SprayParticle {
  x: number
  y: number
  vx: number
  vy: number
  size: number
  alpha: number
  life: number
  maxLife: number
}

export const AnimatedBackground: React.FC<AnimatedBackgroundProps> = ({ theme }) => {
  const canvasRef = useRef<HTMLCanvasElement | null>(null)
  const mouseRef = useRef({ x: 0, y: 0 })
  const dimensionsRef = useRef({ width: 0, height: 0 })
  const themeRef = useRef(theme)

  useEffect(() => {
    themeRef.current = theme
  }, [theme])

  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return

    const ctx = canvas.getContext('2d')
    if (!ctx) return

    let animationFrameId: number
    let time = 0
    let transitionProgress = themeRef.current === 'light' ? 1.0 : 0.0

    // Mouse tracking
    const handleMouseMove = (e: MouseEvent) => {
      mouseRef.current = {
        x: e.clientX,
        y: e.clientY,
      }
    }
    window.addEventListener('mousemove', handleMouseMove)

    // Resize handler
    const handleResize = () => {
      const dpr = window.devicePixelRatio || 1
      const rect = canvas.getBoundingClientRect()
      
      canvas.width = rect.width * dpr
      canvas.height = rect.height * dpr
      dimensionsRef.current = { width: rect.width, height: rect.height }
      ctx.scale(dpr, dpr)
    }

    handleResize()
    window.addEventListener('resize', handleResize)

    // Parallax tracking
    let parallaxX = 0
    let parallaxY = 0

    // Stars
    const stars: Star[] = []
    const generateStars = (w: number, h: number) => {
      stars.length = 0
      const starCount = Math.floor((w * h) / 5000)
      for (let i = 0; i < Math.min(starCount, 300); i++) {
        stars.push({
          x: Math.random() * w,
          y: Math.random() * (h * 0.65),
          size: Math.random() * 1.2 + 0.2,
          alpha: Math.random() * 0.8 + 0.2,
          twinkleSpeed: Math.random() * 0.012 + 0.003,
          twinkleDir: Math.random() > 0.5 ? 1 : -1,
        })
      }
    }

    // Clouds
    const clouds: Cloud[] = []
    const generateClouds = (w: number, h: number) => {
      clouds.length = 0
      const cloudCount = 5
      for (let i = 0; i < cloudCount; i++) {
        const segments: CloudSegment[] = []
        const segmentsCount = Math.floor(Math.random() * 5) + 6
        for (let s = 0; s < segmentsCount; s++) {
          segments.push({
            dx: (Math.random() - 0.5) * 120,
            dy: (Math.random() - 0.5) * 15,
            rx: Math.random() * 40 + 35,
            ry: Math.random() * 15 + 10,
          })
        }
        clouds.push({
          x: Math.random() * w,
          y: Math.random() * (h * 0.35) + h * 0.05,
          speed: Math.random() * 0.05 + 0.02,
          segments,
          opacity: Math.random() * 0.06 + 0.03,
        })
      }
    }

    // Shooting Star
    const shootingStar: ShootingStar = {
      x: 0,
      y: 0,
      length: 0,
      speed: 0,
      angle: 0,
      opacity: 0,
      active: false,
    }

    const triggerShootingStar = (w: number, h: number) => {
      shootingStar.x = Math.random() * (w * 0.6) + w * 0.1
      shootingStar.y = Math.random() * (h * 0.2)
      shootingStar.length = Math.random() * 100 + 60
      shootingStar.speed = Math.random() * 12 + 10
      shootingStar.angle = Math.PI / 6 + Math.random() * (Math.PI / 10)
      shootingStar.opacity = 1.0
      shootingStar.active = true
    }

    // Spray particles
    const sprayParticles: SprayParticle[] = []
    const emitSpray = (x: number, y: number, shipAngle: number, intensity: number, scale: number) => {
      const count = Math.floor(Math.random() * intensity) + 1
      for (let i = 0; i < count; i++) {
        const angle = shipAngle - Math.PI / 6 - Math.random() * (Math.PI / 3)
        const speed = (Math.random() * 2 + 1.5) * scale
        sprayParticles.push({
          x,
          y,
          vx: Math.cos(angle) * speed - 0.4,
          vy: Math.sin(angle) * speed - 0.5,
          size: Math.random() * 1.5 + 0.8,
          alpha: Math.random() * 0.5 + 0.3,
          life: 0,
          maxLife: Math.random() * 25 + 15,
        })
      }
    }

    const { width, height } = dimensionsRef.current
    generateStars(width || window.innerWidth, height || window.innerHeight)
    generateClouds(width || window.innerWidth, height || window.innerHeight)

    // wave equations: PEAKED TROCHOIDAL waves (sharp crests, flat troughs)
    const getWaveHeight = (x: number, layer: number, t: number, h: number) => {
      if (layer === 0) {
        const angle = x * 0.0035 + t * 0.0006
        const waveShape = 1.0 - Math.pow(Math.abs(Math.sin(angle)), 1.5)
        return h * 0.69 + waveShape * 13
      } else if (layer === 1) {
        const angle = x * 0.0055 - t * 0.0009
        const waveShape = 1.0 - Math.pow(Math.abs(Math.sin(angle)), 1.7)
        return h * 0.74 + waveShape * 11
      } else {
        const angle = x * 0.008 + t * 0.0014
        const waveShape = 1.0 - Math.pow(Math.abs(Math.sin(angle)), 2.0)
        return h * 0.795 + waveShape * 10
      }
    }

    // Color lerp
    const lerp = (start: number, end: number, amt: number) => (1 - amt) * start + amt * end

    // Detailed Ship Draw Helper
    const drawRealisticShip = (
      cContext: CanvasRenderingContext2D,
      scale: number,
      shipTime: number,
      opacity: number,
      hasLight: boolean,
      progress: number
    ) => {
      cContext.save()
      cContext.scale(scale, scale)
      
      const baseAlpha = cContext.globalAlpha
      cContext.globalAlpha = baseAlpha * opacity

      // 1. Hull
      cContext.beginPath()
      cContext.moveTo(-45, -3)
      cContext.lineTo(-50, -15)
      cContext.quadraticCurveTo(-25, -17, -15, -11)
      cContext.lineTo(28, -11)
      cContext.quadraticCurveTo(42, -11, 48, -20)
      cContext.lineTo(51, -20)
      cContext.lineTo(44, -3)
      cContext.quadraticCurveTo(0, 8, -45, -3)
      cContext.closePath()

      const hullGrad = cContext.createLinearGradient(-50, -18, 50, 8)
      if (progress < 0.5) {
        hullGrad.addColorStop(0, '#04060b')
        hullGrad.addColorStop(0.3, '#0b111c')
        hullGrad.addColorStop(0.7, '#070a0e')
        hullGrad.addColorStop(1, '#020305')
      } else {
        hullGrad.addColorStop(0, '#1c110a')
        hullGrad.addColorStop(0.4, '#452c1e')
        hullGrad.addColorStop(0.8, '#2a1a12')
        hullGrad.addColorStop(1, '#1c110a')
      }
      cContext.fillStyle = hullGrad
      cContext.fill()

      const strokeR = Math.round(lerp(147, 74, progress))
      const strokeG = Math.round(lerp(197, 85, progress))
      const strokeB = Math.round(lerp(253, 104, progress))
      cContext.strokeStyle = `rgba(${strokeR}, ${strokeG}, ${strokeB}, ${lerp(0.22, 0.4, progress)})`
      cContext.lineWidth = 0.8
      cContext.stroke()

      if (progress > 0.1) {
        cContext.beginPath()
        cContext.moveTo(-48, -12)
        cContext.quadraticCurveTo(-15, -10, 44, -10)
        cContext.strokeStyle = `rgba(255, 255, 255, ${lerp(0, 0.65, progress)})`
        cContext.lineWidth = 0.65
        cContext.stroke()
      }

      // Portholes
      const portAlpha = lerp(0.85, 0.7, progress)
      const portR = Math.round(lerp(253, 30, progress))
      const portG = Math.round(lerp(224, 41, progress))
      const portB = Math.round(lerp(71, 59, progress))
      cContext.fillStyle = `rgba(${portR}, ${portG}, ${portB}, ${portAlpha})`
      
      if (progress < 0.5) {
        cContext.shadowColor = '#fde047'
        cContext.shadowBlur = 4
      }
      const portholeXs = [-35, -25, -15, 0, 10, 20]
      for (const px of portholeXs) {
        cContext.beginPath()
        cContext.arc(px, -6, 1.2, 0, Math.PI * 2)
        cContext.fill()
      }
      cContext.shadowBlur = 0

      cContext.fillRect(-45, -14, 4, 4)

      // 2. Masts & Rigging
      const drawMast = (x: number, yBase: number, mastHeight: number) => {
        cContext.beginPath()
        cContext.moveTo(x - 1, yBase)
        cContext.lineTo(x + 1, yBase)
        cContext.lineTo(x + 0.5, yBase - mastHeight)
        cContext.lineTo(x - 0.5, yBase - mastHeight)
        cContext.closePath()
        cContext.fillStyle = progress < 0.5 ? '#020305' : '#2b1a11'
        cContext.fill()
      }
      drawMast(0, -9, 65)
      drawMast(22, -10, 52)
      drawMast(-22, -12, 48)

      cContext.beginPath()
      cContext.moveTo(40, -11)
      cContext.lineTo(65, -24)
      cContext.strokeStyle = progress < 0.5 ? '#020305' : '#2b1a11'
      cContext.lineWidth = 1.8
      cContext.stroke()

      cContext.beginPath()
      cContext.lineWidth = 0.35
      cContext.strokeStyle = progress < 0.5 ? 'rgba(255, 255, 255, 0.14)' : 'rgba(15, 23, 42, 0.22)'
      for (let offset = -4; offset <= 4; offset += 4) {
        cContext.moveTo(offset, -10); cContext.lineTo(0, -56)
        cContext.moveTo(22 + offset, -10); cContext.lineTo(22, -45)
        cContext.moveTo(-22 + offset, -11); cContext.lineTo(-22, -42)
      }
      cContext.moveTo(-45, -15); cContext.lineTo(-22, -42)
      cContext.moveTo(-22, -42); cContext.lineTo(0, -56)
      cContext.moveTo(0, -56); cContext.lineTo(22, -45)
      cContext.moveTo(22, -45); cContext.lineTo(65, -24)
      cContext.moveTo(22, -32); cContext.lineTo(50, -16)
      cContext.stroke()

      // 3. Sails
      const drawBillowingSail = (sx: number, yBot: number, yTop: number, sw: number, curve: number) => {
        cContext.beginPath()
        cContext.moveTo(sx, yBot)
        cContext.quadraticCurveTo(sx - curve, (yBot + yTop) / 2, sx, yTop)
        cContext.lineTo(sx + sw - 2, yTop + 1)
        cContext.quadraticCurveTo(sx + sw - 2 + curve * 0.4, (yBot + yTop) / 2, sx + sw, yBot + 2)
        cContext.quadraticCurveTo(sx + sw / 2, yBot + 4, sx, yBot)
        cContext.closePath()

        const sailGrad = cContext.createLinearGradient(sx - curve, yTop, sx + sw, yBot)
        if (progress < 0.5) {
          sailGrad.addColorStop(0, 'rgba(224, 242, 254, 0.72)')
          sailGrad.addColorStop(0.35, 'rgba(186, 230, 253, 0.45)')
          sailGrad.addColorStop(0.75, 'rgba(30, 41, 59, 0.65)')
          sailGrad.addColorStop(1, 'rgba(15, 23, 42, 0.85)')
        } else {
          sailGrad.addColorStop(0, 'rgba(255, 255, 253, 0.95)')
          sailGrad.addColorStop(0.3, 'rgba(248, 250, 252, 0.85)')
          sailGrad.addColorStop(0.7, 'rgba(186, 230, 253, 0.55)')
          sailGrad.addColorStop(1, 'rgba(125, 211, 252, 0.65)')
        }
        cContext.fillStyle = sailGrad
        cContext.fill()

        cContext.strokeStyle = progress < 0.5 ? 'rgba(224, 242, 254, 0.22)' : 'rgba(186, 230, 253, 0.4)'
        cContext.lineWidth = 0.5
        cContext.stroke()
      }

      drawBillowingSail(2, -13, -28, 17, 6)
      drawBillowingSail(2, -30, -45, 14, 5)
      drawBillowingSail(1.5, -47, -61, 10, 3.5)

      drawBillowingSail(24, -13, -27, 13, 5)
      drawBillowingSail(24, -29, -43, 11, 4)

      drawBillowingSail(-20, -14, -26, 11, 4)
      drawBillowingSail(-20, -28, -40, 9, 3)

      cContext.beginPath()
      cContext.moveTo(22, -43)
      cContext.quadraticCurveTo(34, -32, 50, -18)
      cContext.quadraticCurveTo(34, -22, 22, -26)
      cContext.closePath()
      const jibGrad = cContext.createLinearGradient(22, -43, 50, -18)
      if (progress < 0.5) {
        jibGrad.addColorStop(0, 'rgba(224, 242, 254, 0.6)')
        jibGrad.addColorStop(1, 'rgba(15, 23, 42, 0.75)')
      } else {
        jibGrad.addColorStop(0, 'rgba(255, 255, 253, 0.9)')
        jibGrad.addColorStop(1, 'rgba(125, 211, 252, 0.55)')
      }
      cContext.fillStyle = jibGrad
      cContext.fill()
      cContext.strokeStyle = progress < 0.5 ? 'rgba(224, 242, 254, 0.18)' : 'rgba(186, 230, 253, 0.35)'
      cContext.stroke()

      cContext.beginPath()
      cContext.moveTo(0, -65)
      const fWave = Math.sin(shipTime * 0.08) * 3
      cContext.lineTo(-12, -63.5 + fWave)
      cContext.lineTo(0, -62)
      cContext.closePath()
      cContext.fillStyle = 'rgba(220, 38, 38, 0.75)'
      cContext.fill()

      const lanternX = -48
      const lanternY = -15
      cContext.beginPath()
      cContext.arc(lanternX, lanternY, 2.5, 0, Math.PI * 2)
      cContext.fillStyle = '#ffea88'
      cContext.fill()

      if (hasLight && progress < 0.55) {
        cContext.save()
        cContext.globalCompositeOperation = 'screen'
        const radG = cContext.createRadialGradient(lanternX, lanternY, 0, lanternX, lanternY, 15)
        radG.addColorStop(0, `rgba(251, 191, 36, ${0.6 * (1.0 - progress)})`)
        radG.addColorStop(1, 'rgba(251, 191, 36, 0)')
        cContext.fillStyle = radG
        cContext.beginPath()
        cContext.arc(lanternX, lanternY, 15, 0, Math.PI * 2)
        cContext.fill()
        cContext.restore()
      }

      cContext.restore()
      cContext.restore()
    }

    // Detailed Pine Tree Drawer
    const drawRealisticPineTree = (
      cContext: CanvasRenderingContext2D,
      tx: number,
      ty: number,
      tScale: number,
      progress: number
    ) => {
      cContext.save()
      cContext.translate(tx, ty)
      cContext.scale(tScale, tScale)

      // Trunk
      cContext.fillStyle = progress < 0.5 ? '#020305' : '#452a1e'
      cContext.fillRect(-2.5, -16, 5, 16)

      cContext.fillStyle = progress < 0.5 ? '#030508' : '#14532d'

      // Layer 1
      cContext.beginPath()
      cContext.moveTo(0, -42)
      cContext.quadraticCurveTo(-9, -29, -9, -27)
      cContext.lineTo(9, -27)
      cContext.quadraticCurveTo(9, -29, 0, -42)
      cContext.closePath()
      cContext.fill()

      // Layer 2
      cContext.beginPath()
      cContext.moveTo(0, -30)
      cContext.quadraticCurveTo(-13, -15, -13, -13)
      cContext.lineTo(13, -13)
      cContext.quadraticCurveTo(13, -15, 0, -30)
      cContext.closePath()
      cContext.fill()

      // Layer 3
      cContext.beginPath()
      cContext.moveTo(0, -16)
      cContext.quadraticCurveTo(-16, -3, -16, -1)
      cContext.lineTo(16, -1)
      cContext.quadraticCurveTo(16, -3, 0, -16)
      cContext.closePath()
      cContext.fill()

      cContext.restore()
    }

    // Detailed Lighthouse Drawer
    const drawLighthouseStructure = (
      cContext: CanvasRenderingContext2D,
      lx: number,
      ly: number,
      lScale: number,
      progress: number
    ) => {
      cContext.save()
      cContext.translate(lx, ly)
      cContext.scale(lScale, lScale)

      cContext.fillStyle = progress < 0.5 ? '#090d16' : '#52525b'
      cContext.fillRect(-12, -6, 24, 6)

      cContext.beginPath()
      cContext.moveTo(-10, -6)
      cContext.lineTo(-7, -80)
      cContext.lineTo(7, -80)
      cContext.lineTo(10, -6)
      cContext.closePath()

      if (progress < 0.5) {
        cContext.fillStyle = '#030508'
        cContext.fill()
      } else {
        cContext.fillStyle = '#f8fafc'
        cContext.fill()

        cContext.save()
        cContext.clip()
        cContext.fillStyle = '#dc2626'
        cContext.fillRect(-12, -32, 24, 18)
        cContext.fillRect(-12, -60, 24, 18)
        cContext.restore()
      }

      cContext.strokeStyle = progress < 0.5 ? 'rgba(255, 255, 255, 0.08)' : '#3f3f46'
      cContext.lineWidth = 0.8
      cContext.stroke()

      cContext.fillStyle = progress < 0.5 ? '#020305' : '#1e293b'
      cContext.fillRect(-9, -84, 18, 4)

      cContext.strokeStyle = progress < 0.5 ? 'rgba(255, 255, 255, 0.28)' : '#475569'
      cContext.lineWidth = 0.8
      cContext.strokeRect(-9, -89, 18, 5)

      if (progress < 0.5) {
        cContext.fillStyle = 'rgba(253, 224, 71, 0.95)'
      } else {
        cContext.fillStyle = 'rgba(186, 230, 253, 0.45)'
      }
      cContext.fillRect(-6, -99, 12, 15)

      cContext.fillStyle = progress < 0.5 ? '#010204' : '#334155'
      cContext.fillRect(-6, -99, 1.2, 15)
      cContext.fillRect(4.8, -99, 1.2, 15)
      cContext.fillRect(-0.6, -99, 1.2, 15)

      cContext.beginPath()
      cContext.arc(0, -99, 6, Math.PI, 0)
      cContext.closePath()
      cContext.fillStyle = progress < 0.5 ? '#010204' : '#991b1b'
      cContext.fill()

      cContext.beginPath()
      cContext.moveTo(0, -105)
      cContext.lineTo(0, -111)
      cContext.strokeStyle = progress < 0.5 ? '#010204' : '#334155'
      cContext.lineWidth = 1
      cContext.stroke()

      if (progress < 0.55) {
        cContext.fillStyle = `rgba(253, 224, 71, ${0.85 * (1.0 - progress)})`
        cContext.fillRect(-1.5, -24, 3, 6)
        cContext.fillRect(-1.2, -52, 2.4, 6)
      }

      cContext.restore()
    }

    // Main Draw Frame
    const draw = () => {
      const w = dimensionsRef.current.width
      const h = dimensionsRef.current.height
      if (w === 0 || h === 0) {
        animationFrameId = requestAnimationFrame(draw)
        return
      }

      time += 1.0

      const targetProgress = themeRef.current === 'light' ? 1.0 : 0.0
      transitionProgress += (targetProgress - transitionProgress) * 0.05

      // Parallax mouse offsets
      const targetParallaxX = (mouseRef.current.x - w / 2) * 0.015
      const targetParallaxY = (mouseRef.current.y - h / 2) * 0.015
      parallaxX += (targetParallaxX - parallaxX) * 0.04
      parallaxY += (targetParallaxY - parallaxY) * 0.04

      ctx.clearRect(0, 0, w, h)

      // ============================================
      // LAYER 0: SKY, STARS, MOON & SUN (PARALLAX 0.2)
      // ============================================
      ctx.save()
      ctx.translate(parallaxX * 0.2, parallaxY * 0.15)

      // Night Sky
      const skyGradDark = ctx.createLinearGradient(0, 0, 0, h * 0.7)
      skyGradDark.addColorStop(0, '#020306')
      skyGradDark.addColorStop(0.35, '#060a14')
      skyGradDark.addColorStop(0.68, '#101827')
      skyGradDark.addColorStop(0.85, '#1e293b')
      ctx.fillStyle = skyGradDark
      ctx.fillRect(0, 0, w, h)

      // Day Sky
      if (transitionProgress > 0.01) {
        ctx.save()
        ctx.globalAlpha = transitionProgress
        const skyGradLight = ctx.createLinearGradient(0, 0, 0, h * 0.7)
        skyGradLight.addColorStop(0, '#38bdf8')
        skyGradLight.addColorStop(0.45, '#bae6fd')
        skyGradLight.addColorStop(0.72, '#f0f9ff')
        skyGradLight.addColorStop(0.9, '#fef08a')
        ctx.fillStyle = skyGradLight
        ctx.fillRect(0, 0, w, h)
        ctx.restore()
      }

      // Stars
      if (transitionProgress < 0.99) {
        ctx.save()
        ctx.globalAlpha = 1.0 - transitionProgress
        for (const star of stars) {
          star.alpha += star.twinkleSpeed * star.twinkleDir
          if (star.alpha >= 1) {
            star.alpha = 1
            star.twinkleDir = -1
          } else if (star.alpha <= 0.15) {
            star.alpha = 0.15
            star.twinkleDir = 1
          }
          ctx.fillStyle = `rgba(255, 255, 255, ${star.alpha})`
          ctx.beginPath()
          ctx.arc(star.x, star.y, star.size, 0, Math.PI * 2)
          ctx.fill()
        }
        ctx.restore()
      }

      // Shooting Star
      if (transitionProgress < 0.8 && shootingStar.active) {
        shootingStar.x += Math.cos(shootingStar.angle) * shootingStar.speed
        shootingStar.y += Math.sin(shootingStar.angle) * shootingStar.speed
        shootingStar.opacity -= 0.016

        if (shootingStar.opacity <= 0) {
          shootingStar.active = false
        } else {
          ctx.save()
          ctx.globalAlpha = (1.0 - transitionProgress) * shootingStar.opacity
          const grad = ctx.createLinearGradient(
            shootingStar.x,
            shootingStar.y,
            shootingStar.x - Math.cos(shootingStar.angle) * shootingStar.length,
            shootingStar.y - Math.sin(shootingStar.angle) * shootingStar.length
          )
          grad.addColorStop(0, 'rgba(255, 255, 255, 1)')
          grad.addColorStop(1, 'rgba(255, 255, 255, 0)')
          ctx.strokeStyle = grad
          ctx.lineWidth = 1.2
          ctx.beginPath()
          ctx.moveTo(shootingStar.x, shootingStar.y)
          ctx.lineTo(
            shootingStar.x - Math.cos(shootingStar.angle) * shootingStar.length,
            shootingStar.y - Math.sin(shootingStar.angle) * shootingStar.length
          )
          ctx.stroke()
          ctx.restore()
        }
      } else if (transitionProgress < 0.8 && Math.random() < 0.001) {
        triggerShootingStar(w, h)
      }

      // Celestial bodies
      const celestialX = w * 0.76
      const celestialY = h * 0.22

      // Draw Moon
      if (transitionProgress < 0.99) {
        const moonRadius = 45
        ctx.save()
        ctx.globalAlpha = 1.0 - transitionProgress

        ctx.save()
        ctx.globalCompositeOperation = 'screen'
        const auraGrad = ctx.createRadialGradient(
          celestialX, celestialY, moonRadius * 0.8,
          celestialX, celestialY, moonRadius * 4.5
        )
        auraGrad.addColorStop(0, 'rgba(224, 242, 254, 0.45)')
        auraGrad.addColorStop(0.18, 'rgba(186, 230, 253, 0.25)')
        auraGrad.addColorStop(0.45, 'rgba(147, 197, 253, 0.07)')
        auraGrad.addColorStop(1, 'rgba(0, 0, 0, 0)')
        ctx.beginPath()
        ctx.arc(celestialX, celestialY, moonRadius * 4.5, 0, Math.PI * 2)
        ctx.fillStyle = auraGrad
        ctx.fill()
        ctx.restore()

        ctx.beginPath()
        ctx.arc(celestialX, celestialY, moonRadius, 0, Math.PI * 2)
        ctx.fillStyle = '#fffdf6'
        ctx.shadowColor = 'rgba(224, 242, 254, 0.5)'
        ctx.shadowBlur = 20
        ctx.fill()
        ctx.shadowBlur = 0

        ctx.save()
        ctx.beginPath()
        ctx.arc(celestialX, celestialY, moonRadius, 0, Math.PI * 2)
        ctx.clip()
        ctx.fillStyle = 'rgba(203, 213, 225, 0.15)'
        const craters = [
          { cx: -18, cy: -10, r: 10 },
          { cx: 12, cy: 15, r: 9 },
          { cx: -8, cy: 20, r: 6 },
          { cx: 20, cy: -10, r: 7 },
          { cx: 5, cy: -18, r: 5 },
          { cx: -22, cy: 8, r: 4 },
        ]
        for (const c of craters) {
          ctx.beginPath()
          ctx.arc(celestialX + c.cx, celestialY + c.cy, c.r, 0, Math.PI * 2)
          ctx.fill()
        }
        ctx.restore()
        ctx.restore()
      }

      // Draw Sun
      if (transitionProgress > 0.01) {
        const sunRadius = 48
        ctx.save()
        ctx.globalAlpha = transitionProgress

        ctx.save()
        ctx.globalCompositeOperation = 'screen'
        ctx.translate(celestialX, celestialY)
        ctx.rotate(time * 0.002)
        const rayCount = 10
        ctx.fillStyle = 'rgba(254, 240, 138, 0.07)'
        for (let i = 0; i < rayCount; i++) {
          ctx.rotate((Math.PI * 2) / rayCount)
          ctx.beginPath()
          ctx.moveTo(0, 0)
          ctx.lineTo(-12, sunRadius * 6)
          ctx.lineTo(12, sunRadius * 6)
          ctx.closePath()
          ctx.fill()
        }
        ctx.restore()

        ctx.save()
        ctx.globalCompositeOperation = 'screen'
        const sunAuraGrad = ctx.createRadialGradient(
          celestialX, celestialY, sunRadius * 0.7,
          celestialX, celestialY, sunRadius * 5.0
        )
        sunAuraGrad.addColorStop(0, 'rgba(255, 253, 224, 0.65)')
        sunAuraGrad.addColorStop(0.2, 'rgba(253, 224, 71, 0.35)')
        sunAuraGrad.addColorStop(0.5, 'rgba(249, 115, 22, 0.08)')
        sunAuraGrad.addColorStop(1, 'rgba(0, 0, 0, 0)')
        ctx.beginPath()
        ctx.arc(celestialX, celestialY, sunRadius * 5.0, 0, Math.PI * 2)
        ctx.fillStyle = sunAuraGrad
        ctx.fill()
        ctx.restore()

        ctx.beginPath()
        ctx.arc(celestialX, celestialY, sunRadius, 0, Math.PI * 2)
        ctx.fillStyle = '#fffdf0'
        ctx.shadowColor = 'rgba(253, 224, 71, 0.8)'
        ctx.shadowBlur = 35
        ctx.fill()
        ctx.shadowBlur = 0
        ctx.restore()
      }
      ctx.restore() // End Sky Layer

      // ============================================
      // LAYER 1: CLOUDS (PARALLAX 0.35)
      // ============================================
      ctx.save()
      ctx.translate(parallaxX * 0.35, parallaxY * 0.25)
      for (const cloud of clouds) {
        cloud.x += cloud.speed
        if (cloud.x > w + 200) {
          cloud.x = -250
          cloud.y = Math.random() * (h * 0.35) + h * 0.05
        }

        ctx.save()
        ctx.globalCompositeOperation = 'screen'
        for (const seg of cloud.segments) {
          const cx = cloud.x + seg.dx
          const cy = cloud.y + seg.dy
          
          const cloudGrad = ctx.createRadialGradient(
            cx, cy, 0,
            cx, cy, seg.rx
          )
          
          const cloudR = Math.round(lerp(186, 255, transitionProgress))
          const cloudG = Math.round(lerp(200, 255, transitionProgress))
          const cloudB = Math.round(lerp(230, 255, transitionProgress))
          const cAlpha = lerp(cloud.opacity, cloud.opacity * 2.2, transitionProgress)

          cloudGrad.addColorStop(0, `rgba(${cloudR}, ${cloudG}, ${cloudB}, ${cAlpha})`)
          cloudGrad.addColorStop(0.5, `rgba(${Math.round(lerp(148, 224, transitionProgress))}, ${Math.round(lerp(163, 235, transitionProgress))}, ${Math.round(lerp(184, 254, transitionProgress))}, ${cAlpha * 0.4})`)
          cloudGrad.addColorStop(1, 'rgba(0, 0, 0, 0)')

          ctx.fillStyle = cloudGrad
          ctx.beginPath()
          ctx.ellipse(cx, cy, seg.rx, seg.ry, 0, 0, Math.PI * 2)
          ctx.fill()
        }
        ctx.restore()
      }
      ctx.restore() // End Clouds

      // ============================================
      // LAYER 2: DISTANT WAVES & 2 DISTANT SHIPS (PARALLAX 0.5)
      // ============================================
      ctx.save()
      ctx.translate(parallaxX * 0.5, parallaxY * 0.35)

      // Fills Wave 0
      const drawWaveLayer = (layer: number, startGradY: number, colorNight0: string, colorNight1: string, colorDay0: string, colorDay1: string) => {
        ctx.beginPath()
        ctx.moveTo(0, h)
        for (let x = 0; x <= w + 10; x += 10) {
          ctx.lineTo(x, getWaveHeight(x, layer, time, h))
        }
        ctx.lineTo(w, h)
        ctx.closePath()

        const wGradDark = ctx.createLinearGradient(0, startGradY, 0, h)
        wGradDark.addColorStop(0, colorNight0)
        wGradDark.addColorStop(1, colorNight1)
        ctx.fillStyle = wGradDark
        ctx.fill()

        if (transitionProgress > 0.01) {
          ctx.save()
          ctx.globalAlpha = transitionProgress
          const wGradLight = ctx.createLinearGradient(0, startGradY, 0, h)
          wGradLight.addColorStop(0, colorDay0)
          wGradLight.addColorStop(1, colorDay1)
          ctx.fillStyle = wGradLight
          ctx.beginPath()
          ctx.moveTo(0, h)
          for (let x = 0; x <= w + 10; x += 10) {
            ctx.lineTo(x, getWaveHeight(x, layer, time, h))
          }
          ctx.lineTo(w, h)
          ctx.closePath()
          ctx.fill()
          ctx.restore()
        }

        // Draw realistic, soft wave crest highlights (No cartoonish hand-drawn lines on faces)
        ctx.strokeStyle = transitionProgress < 0.5 ? 'rgba(224, 242, 254, 0.04)' : 'rgba(255, 255, 255, 0.06)'
        ctx.lineWidth = 0.6
        ctx.beginPath()
        for (let x = 0; x <= w; x += 8) {
          const wy = getWaveHeight(x, layer, time, h)
          if (x === 0) ctx.moveTo(x, wy)
          else ctx.lineTo(x, wy)
        }
        ctx.stroke()
      }

      drawWaveLayer(0, h * 0.6, '#0c1524', '#03060a', '#0284c7', '#0369a1')

      // Distant Ship 1
      const ship0Scale = 0.12 * Math.max(0.6, Math.min(1.0, w / 1200))
      const ship0Speed = time * 0.05
      const ship0X = (ship0Speed + w * 0.1) % (w + 120) - 60
      const ship0Y = getWaveHeight(ship0X, 0, time, h) - 1.2

      const ship0Left = getWaveHeight(ship0X - 4, 0, time, h)
      const ship0Right = getWaveHeight(ship0X + 4, 0, time, h)
      const ship0Angle = Math.atan2(ship0Right - ship0Left, 8) + Math.sin(time * 0.03) * 0.01

      ctx.save()
      ctx.translate(ship0X, ship0Y)
      ctx.rotate(ship0Angle)
      drawRealisticShip(ctx, ship0Scale, time, 0.32, false, transitionProgress)
      ctx.restore()

      // Distant Ship 2
      const ship1Scale = 0.15 * Math.max(0.6, Math.min(1.0, w / 1200))
      const ship1Speed = time * 0.065
      const ship1X = (ship1Speed + w * 0.65) % (w + 120) - 60
      const ship1Y = getWaveHeight(ship1X, 0, time, h) - 1.5

      const ship1Left = getWaveHeight(ship1X - 4, 0, time, h)
      const ship1Right = getWaveHeight(ship1X + 4, 0, time, h)
      const ship1Angle = Math.atan2(ship1Right - ship1Left, 8) + Math.sin(time * 0.035) * 0.01

      ctx.save()
      ctx.translate(ship1X, ship1Y)
      ctx.rotate(ship1Angle)
      drawRealisticShip(ctx, ship1Scale, time, 0.35, false, transitionProgress)
      ctx.restore()
      ctx.restore() // End Wave 0 Layer



      // ============================================
      // LAYER 3: MIDGROUND WAVES & 2 MERCHANT SHIPS (PARALLAX 0.7)
      // ============================================
      ctx.save()
      ctx.translate(parallaxX * 0.7, parallaxY * 0.45)

      // Wave 1 Fills
      drawWaveLayer(1, h * 0.65, '#060c16', '#010305', '#0369a1', '#075985')

      // Midground Ship 1
      const ship2Scale = 0.32 * Math.max(0.6, Math.min(1.0, w / 1200))
      const ship2Speed = time * 0.10
      const ship2X = (ship2Speed + w * 0.1) % (w + 180) - 90
      const ship2Y = getWaveHeight(ship2X, 1, time, h) - 1.0

      const ship2Left = getWaveHeight(ship2X - 7, 1, time, h)
      const ship2Right = getWaveHeight(ship2X + 7, 1, time, h)
      const ship2Angle = Math.atan2(ship2Right - ship2Left, 14) + Math.sin(time * 0.04) * 0.015

      const s2BowX = ship2X + Math.cos(ship2Angle) * (38 * ship2Scale)
      const s2BowY = ship2Y + Math.sin(ship2Angle) * (38 * ship2Scale)
      emitSpray(s2BowX, s2BowY, ship2Angle, 1, ship2Scale)

      ctx.save()
      ctx.translate(ship2X, ship2Y)
      ctx.rotate(ship2Angle)
      drawRealisticShip(ctx, ship2Scale, time, 0.72, true, transitionProgress)
      ctx.restore()

      // Midground Ship 2
      const ship3Scale = 0.35 * Math.max(0.6, Math.min(1.0, w / 1200))
      const ship3Speed = time * 0.13
      const ship3X = (ship3Speed + w * 0.6) % (w + 180) - 90
      const ship3Y = getWaveHeight(ship3X, 1, time, h) - 1.2

      const ship3Left = getWaveHeight(ship3X - 7, 1, time, h)
      const ship3Right = getWaveHeight(ship3X + 7, 1, time, h)
      const ship3Angle = Math.atan2(ship3Right - ship3Left, 14) + Math.sin(time * 0.038) * 0.018

      const s3BowX = ship3X + Math.cos(ship3Angle) * (38 * ship3Scale)
      const s3BowY = ship3Y + Math.sin(ship3Angle) * (38 * ship3Scale)
      emitSpray(s3BowX, s3BowY, ship3Angle, 1, ship3Scale)

      ctx.save()
      ctx.translate(ship3X, ship3Y)
      ctx.rotate(ship3Angle)
      drawRealisticShip(ctx, ship3Scale, time, 0.75, true, transitionProgress)
      ctx.restore()
      ctx.restore() // End Wave 1 Layer


      // ============================================
      // LAYER 4: FOREGROUND WAVES & FLAGSHIP (PARALLAX 0.95)
      // ============================================
      ctx.save()
      for (let i = sprayParticles.length - 1; i >= 0; i--) {
        const p = sprayParticles[i]
        p.x += p.vx
        p.y += p.vy
        p.vy += 0.045
        p.life++
        
        const pAlpha = lerp(0.45, 0.65, transitionProgress)
        p.alpha = (1 - p.life / p.maxLife) * pAlpha

        if (p.life >= p.maxLife || p.alpha <= 0) {
          sprayParticles.splice(i, 1)
        } else {
          ctx.fillStyle = `rgba(255, 255, 255, ${p.alpha})`
          ctx.beginPath()
          ctx.arc(p.x, p.y, p.size, 0, Math.PI * 2)
          ctx.fill()
        }
      }
      ctx.restore()

      // Wave 2 Fills
      ctx.save()
      ctx.translate(parallaxX * 0.95, parallaxY * 0.55)
      drawWaveLayer(2, h * 0.72, '#030508', '#000000', '#075985', '#0c4a6e')

      // Foreground Flagship (Ship 4)
      const ship4Scale = 0.76 * Math.max(0.6, Math.min(1.0, w / 1200))
      const ship4Speed = time * 0.18
      const ship4X = (ship4Speed + w * 0.15) % (w + 240) - 120
      const ship4Y = getWaveHeight(ship4X, 2, time, h) - 0.5

      const ship4Left = getWaveHeight(ship4X - 12, 2, time, h)
      const ship4Right = getWaveHeight(ship4X + 12, 2, time, h)
      const ship4Angle = Math.atan2(ship4Right - ship4Left, 24) + Math.sin(time * 0.042) * 0.012

      const s4BowX = ship4X + Math.cos(ship4Angle) * (44 * ship4Scale)
      const s4BowY = ship4Y + Math.sin(ship4Angle) * (44 * ship4Scale)
      emitSpray(s4BowX, s4BowY, ship4Angle, 2.5, ship4Scale)

      ctx.save()
      ctx.translate(ship4X, ship4Y)
      ctx.rotate(ship4Angle)
      drawRealisticShip(ctx, ship4Scale, time, 1.0, true, transitionProgress)
      ctx.restore()

      // Flagship Searchlight
      if (transitionProgress < 0.98) {
        const lScaleX = 35 * ship4Scale
        const lScaleY = -12 * ship4Scale
        const lanternWorldX = ship4X + Math.cos(ship4Angle) * lScaleX - Math.sin(ship4Angle) * lScaleY
        const lanternWorldY = ship4Y + Math.sin(ship4Angle) * lScaleX + Math.cos(ship4Angle) * lScaleY

        const beamSway = Math.sin(time * 0.035) * 0.07
        const beamAngle = ship4Angle + 0.38 + beamSway
        const beamLength = 250 * ship4Scale
        const beamSpread = 0.18

        const p1x = lanternWorldX + Math.cos(beamAngle - beamSpread) * beamLength
        const p1y = lanternWorldY + Math.sin(beamAngle - beamSpread) * beamLength
        const p2x = lanternWorldX + Math.cos(beamAngle + beamSpread) * beamLength
        const p2y = lanternWorldY + Math.sin(beamAngle + beamSpread) * beamLength

        ctx.save()
        ctx.globalAlpha = 1.0 - transitionProgress
        ctx.globalCompositeOperation = 'screen'
        
        ctx.beginPath()
        ctx.moveTo(lanternWorldX, lanternWorldY)
        ctx.lineTo(p1x, p1y)
        ctx.lineTo(p2x, p2y)
        ctx.closePath()

        const beamGrad = ctx.createRadialGradient(
          lanternWorldX, lanternWorldY, 2,
          lanternWorldX, lanternWorldY, beamLength
        )
        beamGrad.addColorStop(0, 'rgba(254, 240, 138, 0.28)')
        beamGrad.addColorStop(0.35, 'rgba(254, 240, 138, 0.12)')
        beamGrad.addColorStop(1, 'rgba(254, 240, 138, 0)')
        ctx.fillStyle = beamGrad
        ctx.fill()

        const targetWaterY = h * 0.76
        const distToWater = (targetWaterY - lanternWorldY) / Math.sin(beamAngle)
        if (distToWater > 0 && distToWater < beamLength) {
          const spotX = lanternWorldX + Math.cos(beamAngle) * distToWater
          if (spotX > 0 && spotX < w) {
            const spotY = getWaveHeight(spotX, 2, time, h)
            ctx.beginPath()
            ctx.ellipse(spotX, spotY, 24 * ship4Scale, 5 * ship4Scale, ship4Angle, 0, Math.PI * 2)
            const spotGrad = ctx.createRadialGradient(
              spotX, spotY, 0,
              spotX, spotY, 24 * ship4Scale
            )
            spotGrad.addColorStop(0, 'rgba(254, 240, 138, 0.48)')
            spotGrad.addColorStop(0.4, 'rgba(254, 240, 138, 0.18)')
            spotGrad.addColorStop(1, 'rgba(254, 240, 138, 0)')
            ctx.fillStyle = spotGrad
            ctx.fill()
          }
        }
        ctx.restore()
      }
      ctx.restore() // End Wave 2 Layer


      // ============================================
      // LAYER 5: FOREGROUND LEFT LAND & LIGHTHOUSE (PARALLAX 1.35)
      // ============================================
      const landXOffset = parallaxX * 1.35
      const landYOffset = parallaxY * 0.75

      const lighthouseX = w * 0.18 + landXOffset
      const lighthouseY = h * 0.83 + landYOffset
      const lighthouseScale = 0.45 * Math.max(0.6, Math.min(1.0, w / 1200))

      // 1. Rotating Lighthouse Beacons
      if (transitionProgress < 0.98) {
        ctx.save()
        ctx.globalAlpha = 1.0 - transitionProgress
        ctx.globalCompositeOperation = 'screen'

        const beaconWorldX = lighthouseX
        const beaconWorldY = lighthouseY - 99 * lighthouseScale

        const beaconSpeed = time * 0.006
        const beamAngle1 = beaconSpeed
        const beamAngle2 = beaconSpeed + Math.PI

        const drawBeaconBeam = (angle: number) => {
          const lLength = w * 0.85
          const lSpread = 0.14
          
          const pA1x = beaconWorldX + Math.cos(angle - lSpread) * lLength
          const pA1y = beaconWorldY + Math.sin(angle - lSpread) * lLength
          const pA2x = beaconWorldX + Math.cos(angle + lSpread) * lLength
          const pA2y = beaconWorldY + Math.sin(angle + lSpread) * lLength

          ctx.beginPath()
          ctx.moveTo(beaconWorldX, beaconWorldY)
          ctx.lineTo(pA1x, pA1y)
          ctx.lineTo(pA2x, pA2y)
          ctx.closePath()

          const radG = ctx.createRadialGradient(
            beaconWorldX, beaconWorldY, 5,
            beaconWorldX, beaconWorldY, lLength
          )
          radG.addColorStop(0, 'rgba(254, 240, 138, 0.42)')
          radG.addColorStop(0.2, 'rgba(254, 240, 138, 0.18)')
          radG.addColorStop(0.6, 'rgba(254, 240, 138, 0.03)')
          radG.addColorStop(1, 'rgba(0, 0, 0, 0)')
          ctx.fillStyle = radG
          ctx.fill()
        }

        drawBeaconBeam(beamAngle1)
        drawBeaconBeam(beamAngle2)
        ctx.restore()
      }

      // 2. Draw Left Landmass
      ctx.save()
      
      // Night Left Land (Base silhouette)
      ctx.beginPath()
      ctx.moveTo(-100 + landXOffset, h)
      ctx.lineTo(-100 + landXOffset, h * 0.84 + landYOffset)
      ctx.quadraticCurveTo(w * 0.08 + landXOffset, h * 0.84 + landYOffset, w * 0.18 + landXOffset, h * 0.83 + landYOffset)
      ctx.quadraticCurveTo(w * 0.28 + landXOffset, h * 0.85 + landYOffset, w * 0.38 + landXOffset, h * 0.93 + landYOffset)
      ctx.lineTo(w * 0.46 + landXOffset, h)
      ctx.closePath()
      ctx.fillStyle = '#020305'
      ctx.fill()

      // Day Left Land
      if (transitionProgress > 0.01) {
        ctx.save()
        ctx.globalAlpha = transitionProgress
        ctx.beginPath()
        ctx.moveTo(-100 + landXOffset, h)
        ctx.lineTo(-100 + landXOffset, h * 0.84 + landYOffset)
        ctx.quadraticCurveTo(w * 0.08 + landXOffset, h * 0.84 + landYOffset, w * 0.18 + landXOffset, h * 0.83 + landYOffset)
        ctx.quadraticCurveTo(w * 0.28 + landXOffset, h * 0.85 + landYOffset, w * 0.38 + landXOffset, h * 0.93 + landYOffset)
        ctx.lineTo(w * 0.46 + landXOffset, h)
        ctx.closePath()
        
        const landGrad = ctx.createLinearGradient(0, h * 0.8 + landYOffset, 0, h)
        landGrad.addColorStop(0, '#5c4033')
        landGrad.addColorStop(0.5, '#3a2d27')
        landGrad.addColorStop(1, '#1f1510')
        ctx.fillStyle = landGrad
        ctx.fill()

        ctx.strokeStyle = 'rgba(254, 240, 138, 0.15)'
        ctx.lineWidth = 1.0
        ctx.stroke()
        ctx.restore()
      }

      // Left Pine Trees
      drawRealisticPineTree(ctx, w * 0.04 + landXOffset, h * 0.84 + landYOffset, 0.44 * Math.max(0.6, Math.min(1.0, w / 1200)), transitionProgress)
      drawRealisticPineTree(ctx, w * 0.12 + landXOffset, h * 0.83 + landYOffset, 0.48 * Math.max(0.6, Math.min(1.0, w / 1200)), transitionProgress)
      drawRealisticPineTree(ctx, w * 0.30 + landXOffset, h * 0.86 + landYOffset, 0.40 * Math.max(0.6, Math.min(1.0, w / 1200)), transitionProgress)

      // Lighthouse
      drawLighthouseStructure(ctx, lighthouseX, lighthouseY, lighthouseScale, transitionProgress)
      ctx.restore()

      animationFrameId = requestAnimationFrame(draw)
    }

    draw()

    return () => {
      window.removeEventListener('mousemove', handleMouseMove)
      window.removeEventListener('resize', handleResize)
      cancelAnimationFrame(animationFrameId)
    }
  }, [])

  return (
    <canvas
      ref={canvasRef}
      style={{
        position: 'fixed',
        top: 0,
        left: 0,
        width: '100%',
        height: '100%',
        zIndex: 0,
        pointerEvents: 'none',
        display: 'block',
      }}
    />
  )
}
