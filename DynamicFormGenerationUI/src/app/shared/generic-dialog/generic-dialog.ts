import { CommonModule, DOCUMENT } from '@angular/common';
import {
  AfterViewInit,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnDestroy,
  Output,
  ViewChild,
  inject,
  signal
} from '@angular/core';

export type GenericDialogType = 'confirmation' | 'information' | 'warning' | 'error';
export type GenericDialogVariant = 'primary' | 'secondary' | 'success' | 'danger' | 'warning' | 'info' | 'light' | 'dark' | 'outline-primary' | 'outline-secondary' | 'outline-danger';

/** Describes one action button and its optional callback. */
export interface GenericDialogButton {
  action: string;
  label: string;
  variant?: GenericDialogVariant;
  visible?: boolean;
  closeOnClick?: boolean;
  callback?: () => void | Promise<void>;
}

/** Configuration for the reusable dialog. */
export interface GenericDialogConfig {
  title: string;
  message?: string;
  type?: GenericDialogType;
  buttons: GenericDialogButton[];
  closeOnEscape?: boolean;
  closeOnBackdrop?: boolean;
}

@Component({
  selector: 'app-generic-dialog',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './generic-dialog.html',
  styleUrl: './generic-dialog.scss'
})
export class GenericDialog implements AfterViewInit, OnDestroy {
  @Input({ required: true }) config!: GenericDialogConfig;
  /** Emits after the dialog has been added to the view and initialized. */
  @Output() show = new EventEmitter<void>();
  /** Emits immediately before the dialog is removed from the view. */
  @Output() hide = new EventEmitter<void>();
  /** Emits when the dialog is dismissed or an action closes it. */
  @Output() closed = new EventEmitter<void>();
  /** Emits the configured action identifier after its callback completes. */
  @Output() actionSelected = new EventEmitter<string>();
  /** Emits callback failures so the consumer can report or recover from them. */
  @Output() actionError = new EventEmitter<{ action: string; error: unknown }>();
  @ViewChild('dialogElement') private dialogElement?: ElementRef<HTMLElement>;
  private static nextId = 0;
  readonly titleId = `generic-dialog-title-${GenericDialog.nextId++}`;
  readonly processing = signal(false);
  private readonly document = inject(DOCUMENT);
  private readonly previouslyFocused = this.document.activeElement
    && typeof (this.document.activeElement as HTMLElement).focus === 'function'
    ? this.document.activeElement as HTMLElement
    : null;


  ngAfterViewInit(): void {
    const dialog = this.dialogElement?.nativeElement;
    const initialTarget = dialog?.querySelector<HTMLElement>(
      'button:not([disabled]):not([hidden]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
    );
    (initialTarget ?? dialog)?.focus();
    this.show.emit();
  }

  ngOnDestroy(): void {
    this.hide.emit();
    this.previouslyFocused?.focus();
  }

  async selectAction(button: GenericDialogButton): Promise<void> {
    if (this.processing()) return;

    this.processing.set(true);
    try {
      await button.callback?.();
      this.actionSelected.emit(button.action);
      if (button.closeOnClick !== false) this.closed.emit();
    } catch (error) {
      this.actionError.emit({ action: button.action, error });
    } finally {
      this.processing.set(false);
    }
  }

  onBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget && this.config.closeOnBackdrop !== false && !this.processing()) {
      this.closed.emit();
    }
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      if (this.config.closeOnEscape !== false && !this.processing()) this.closed.emit();
      return;
    }

    if (event.key !== 'Tab') return;
    const dialog = this.dialogElement?.nativeElement;
    if (!dialog) return;

    const focusable = Array.from(dialog.querySelectorAll<HTMLElement>(
      'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
    )).filter(element => element.offsetParent !== null);
    if (focusable.length === 0) {
      event.preventDefault();
      dialog.focus();
      return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (event.shiftKey && this.document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && this.document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }
}
