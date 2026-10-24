import { Component, effect, inject, input, signal } from '@angular/core';
import { CareService } from './care.service';
import { Icon } from './icon';

@Component({
  selector: 'app-preview',
  imports: [Icon],
  template: `
    <span class="shot">
      @if (url(); as src) {
        <img [src]="src" alt="" />
      } @else {
        <app-icon [name]="image() ? 'image' : 'file'" />
      }
    </span>
  `,
  styles: `
    :host {
      display: inline-flex;
      width: 2.5rem;
      height: 2.5rem;
      flex: none;
    }
    .shot {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 100%;
      height: 100%;
      overflow: hidden;
      border-radius: 8px;
      background: #eef3f9;
      color: var(--blue);
    }
    img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }
  `,
})
export class FilePreview {
  readonly fileName = input.required<string>();
  readonly recordId = input.required<string>();
  readonly requestId = input<string | null>(null);
  private readonly care = inject(CareService);
  readonly url = signal<string | null>(null);

  constructor() {
    effect((onCleanup) => {
      const name = this.fileName();
      const recordId = this.recordId();
      const requestId = this.requestId();
      this.url.set(null);
      if (!isImage(name)) {
        return;
      }

      let objectUrl: string | null = null;
      const request = requestId
        ? this.care.openOpinionFile(requestId, recordId)
        : this.care.openRecord(recordId);
      const subscription = request.subscribe({
        next: (blob) => {
          if (!blob.type.startsWith('image/')) {
            return;
          }
          objectUrl = URL.createObjectURL(blob);
          this.url.set(objectUrl);
        },
      });
      onCleanup(() => {
        subscription.unsubscribe();
        if (objectUrl) {
          URL.revokeObjectURL(objectUrl);
        }
      });
    });
  }

  image(): boolean {
    return isImage(this.fileName());
  }
}

function isImage(name: string): boolean {
  return /\.(jpe?g|png|webp)$/i.test(name);
}
