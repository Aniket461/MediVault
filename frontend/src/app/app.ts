import { Component, ElementRef, HostListener, effect, inject, signal, viewChild } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AccountService } from './account.service';
import { AuthService } from './auth.service';
import { Icon } from './icon';
import { NoticeInbox } from './notice-inbox';
import { ProfileDialog } from './profile.dialog';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Icon, ProfileDialog],
  templateUrl: './app.html',
})
export class App {
  readonly auth = inject(AuthService);
  readonly accounts = inject(AccountService);
  readonly inbox = inject(NoticeInbox);
  private readonly router = inject(Router);
  readonly menuOpen = signal(false);
  readonly noticesOpen = signal(false);
  private readonly menuRoot = viewChild<ElementRef<HTMLElement>>('menuRoot');

  constructor() {
    if (this.auth.token() && !this.auth.user()) {
      this.auth.loadMe().subscribe({ error: () => this.auth.logout() });
    }

    effect(() => {
      const user = this.auth.user();
      if (user && user.role !== 'Admin') {
        this.inbox.refresh();
      } else {
        this.inbox.reset();
      }
    });
  }

  toggleMenu(): void {
    const open = !this.menuOpen();
    this.menuOpen.set(open);
    if (open && this.auth.user()?.role !== 'Admin') {
      this.inbox.refresh();
    }
  }

  closeMenu(): void {
    this.menuOpen.set(false);
  }

  openAccount(): void {
    this.closeMenu();
    this.accounts.openSelf();
  }

  askDoctor(): void {
    this.closeMenu();
    void this.router.navigate(['/requests'], { queryParams: { ask: Date.now() } });
  }

  openNotices(): void {
    this.closeMenu();
    this.inbox.refresh();
    this.noticesOpen.set(true);
  }

  closeNotices(): void {
    this.noticesOpen.set(false);
  }

  signOut(): void {
    this.closeMenu();
    this.auth.logout();
    void this.router.navigateByUrl('/login');
  }

  @HostListener('document:click', ['$event'])
  closeMenuOnOutsideClick(event: MouseEvent): void {
    if (!this.menuOpen()) {
      return;
    }

    const root = this.menuRoot()?.nativeElement;
    if (root && !root.contains(event.target as Node)) {
      this.closeMenu();
    }
  }

  @HostListener('document:keydown.escape')
  closeMenuOnEscape(): void {
    if (this.noticesOpen()) {
      this.closeNotices();
      return;
    }

    this.closeMenu();
  }
}
