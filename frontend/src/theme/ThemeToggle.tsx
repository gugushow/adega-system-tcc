import { Dropdown } from 'react-bootstrap'
import type { ThemePreference } from './themeStorage'
import { useTheme } from './useTheme'

const options: { value: ThemePreference; label: string; icon: string }[] = [
  { value: 'light', label: 'Claro', icon: 'bi-sun' },
  { value: 'dark', label: 'Escuro', icon: 'bi-moon-stars' },
  { value: 'system', label: 'Sistema', icon: 'bi-laptop' },
]

/** Menu da barra superior para escolher o tema. A troca vale na hora e fica salva no navegador. */
export function ThemeToggle() {
  const { preference, setPreference } = useTheme()
  const current = options.find((option) => option.value === preference) ?? options[2]

  return (
    <Dropdown align="end">
      <Dropdown.Toggle variant="outline-secondary" size="sm" className="topbar-button" aria-label={`Tema: ${current.label}`}>
        <i className={`bi ${current.icon}`} aria-hidden="true" />
        <span className="d-none d-md-inline ms-1">{current.label}</span>
      </Dropdown.Toggle>
      <Dropdown.Menu>
        <Dropdown.Header>Tema</Dropdown.Header>
        {options.map((option) => (
          <Dropdown.Item
            key={option.value}
            as="button"
            active={option.value === preference}
            aria-pressed={option.value === preference}
            onClick={() => setPreference(option.value)}
          >
            <i className={`bi ${option.icon} me-2`} aria-hidden="true" />
            {option.label}
            {option.value === preference && <i className="bi bi-check2 ms-2" aria-hidden="true" />}
          </Dropdown.Item>
        ))}
      </Dropdown.Menu>
    </Dropdown>
  )
}
