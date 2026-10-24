import { Component, input } from '@angular/core';

@Component({
  selector: 'app-icon',
  template: `
    <svg viewBox="0 0 24 24" aria-hidden="true">
      @switch (name()) {
        @case ('vault') {
          <path d="M12 3 5 6v6c0 4.2 2.9 7.2 7 8.4 4.1-1.2 7-4.2 7-8.4V6z" />
          <path d="M9.5 12.2 11.2 14l3.3-3.6" />
        }
        @case ('user') {
          <circle cx="12" cy="8" r="3" />
          <path d="M5.5 19.2c1.4-2.8 3.5-4.2 6.5-4.2s5.1 1.4 6.5 4.2" />
        }
        @case ('mail') {
          <rect x="3.5" y="5.5" width="17" height="13" rx="2" />
          <path d="m4.5 7.5 7.5 5.5 7.5-5.5" />
        }
        @case ('lock') {
          <rect x="5" y="11" width="14" height="8.5" rx="2" />
          <path d="M8 11V8.2a4 4 0 0 1 8 0V11" />
        }
        @case ('doctor') {
          <circle cx="10" cy="8" r="2.7" />
          <path d="M4.8 18.5c1.1-2.4 2.8-3.6 5.2-3.6 1.3 0 2.4.4 3.3 1.1" />
          <path d="M16 13.5v6M13.2 16.5H18.8" />
        }
        @case ('fee') {
          <circle cx="12" cy="12" r="8" />
          <path d="M12 7.5v9" />
          <path d="M9.6 9.4c.5-.7 1.4-1.1 2.4-1.1 1.5 0 2.4.8 2.4 1.9s-.9 1.7-2.4 1.9-2.4.7-2.4 1.9.9 1.9 2.4 1.9c1 0 1.9-.4 2.4-1.1" />
        }
        @case ('euro') {
          <path d="M17.2 7.6A6.4 6.4 0 0 0 6.6 12a6.4 6.4 0 0 0 10.6 4.4" />
          <path d="M5.2 10.3h9.2M5.2 13.7h9.2" />
        }
        @case ('check') {
          <path d="M5 12.5 9.2 17 19 7" />
        }
        @case ('x') {
          <path d="M7 7l10 10M17 7 7 17" />
        }
        @case ('logout') {
          <path d="M10 6.5V4.5h9v15h-9v-2" />
          <path d="M13.5 12H4.5m0 0 2.8-2.8M4.5 12l2.8 2.8" />
        }
        @case ('list') {
          <rect x="6" y="3.8" width="12" height="16.4" rx="2" />
          <path d="M9 3.8h6v2.4H9zM9 10.2h6M9 14h4" />
        }
        @case ('phone') {
          <path d="M8.2 4.8h2.1l.9 2.6-1.5.9a10.2 10.2 0 0 0 5 5l.9-1.5 2.6.9v2.1a1.4 1.4 0 0 1-1.5 1.4A13.2 13.2 0 0 1 6.8 6.3a1.4 1.4 0 0 1 1.4-1.5z" />
        }
        @case ('home') {
          <path d="M4.5 11 12 4.5 19.5 11" />
          <path d="M7 10.4V19h10v-8.6" />
        }
        @case ('hospital') {
          <path d="M5 20V9l7-4.5L19 9v11" />
          <path d="M12 10.5v6M9.2 13.5h5.6" />
        }
        @case ('file') {
          <path d="M7 3.5h6.2L18.5 8.8V20a1.5 1.5 0 0 1-1.5 1.5H7A1.5 1.5 0 0 1 5.5 20V5A1.5 1.5 0 0 1 7 3.5z" />
          <path d="M13 3.8V9h5.2" />
        }
        @case ('trash') {
          <path d="M5 7.5h14" />
          <path d="M9.2 7.4V5.4h5.6v2" />
          <path d="M8.2 7.5l.7 11.5h6.2l.7-11.5" />
        }
        @case ('menu') {
          <path d="M4 7h16M4 12h16M4 17h16" />
        }
        @case ('open') {
          <path d="M10 5.5H6.8A2 2 0 0 0 4.8 7.5v9.7a2 2 0 0 0 2 2h9.7a2 2 0 0 0 2-2V14" />
          <path d="M13 4.5h6.5V11" />
          <path d="M19.2 4.8 11 13" />
        }
        @case ('archive') {
          <path d="M4 7.2h16" />
          <path d="M5.4 7.2V18a1.5 1.5 0 0 0 1.5 1.5h10.2A1.5 1.5 0 0 0 18.6 18V7.2" />
          <path d="M4 4.2h16v3H4z" />
          <path d="M10 12.2h4" />
        }
        @case ('edit') {
          <path d="M4.5 19.5h4.2L19.2 9a2 2 0 0 0-2.8-2.8L5.9 16.7v2.8z" />
          <path d="m14.2 6.4 2.8 2.8" />
        }
        @case ('image') {
          <rect x="4" y="5" width="16" height="14" rx="2" />
          <circle cx="9" cy="10" r="1.3" />
          <path d="m4.8 16.4 4.1-3.5 3.1 2.5 2.1-1.7 5.1 4.1" />
        }
        @case ('help') {
          <circle cx="12" cy="12" r="8" />
          <path d="M9.4 9.3a2.6 2.6 0 0 1 4.9 1.2c0 1.7-2.3 2-2.3 3.4" />
          <circle cx="12" cy="16.8" r="0.7" fill="currentColor" stroke="none" />
        }
        @case ('bell') {
          <path d="M6.4 16.2V11a5.6 5.6 0 0 1 11.2 0v5.2" />
          <path d="M4.8 16.2h14.4" />
          <path d="M10 18.5a2 2 0 0 0 4 0" />
        }
      }
    </svg>
  `,
  styles: `
    :host {
      display: inline-flex;
      width: 1.15rem;
      height: 1.15rem;
      flex: none;
    }
    svg {
      width: 100%;
      height: 100%;
      fill: none;
      stroke: currentColor;
      stroke-width: 1.75;
      stroke-linecap: round;
      stroke-linejoin: round;
    }
  `,
})
export class Icon {
  readonly name = input.required<
    'vault' | 'user' | 'mail' | 'lock' | 'doctor' | 'fee' | 'euro' | 'check' | 'x' | 'logout' | 'list' | 'phone' | 'home' | 'hospital' | 'file' | 'trash' | 'menu' | 'open' | 'archive' | 'edit' | 'image' | 'help' | 'bell'
  >();
}
