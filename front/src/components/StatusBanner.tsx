import type { ReactNode } from 'react'
import React from 'react'

void React

type StatusBannerProps = {
  tone: 'error' | 'success' | 'info'
  children: ReactNode
}

export function StatusBanner({ tone, children }: StatusBannerProps) {
  return <div className={`status-banner ${tone}`} role={tone === 'error' ? 'alert' : 'status'}>{children}</div>
}
