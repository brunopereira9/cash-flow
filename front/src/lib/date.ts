const businessTimeZone = 'America/Sao_Paulo'

export function currentBusinessDate(): string {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: businessTimeZone,
  }).format(new Date())
}

export function addBusinessDays(date: string, days: number): string {
  const value = new Date(`${date}T12:00:00-03:00`)
  value.setDate(value.getDate() + days)
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: businessTimeZone,
  }).format(value)
}
