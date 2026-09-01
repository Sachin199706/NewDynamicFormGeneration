import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-pagination',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './pagination.html',
  styleUrl: './pagination.scss',
})
export class Pagination {
  @Input() inumCurrentPage = 1;
  @Input() inumTotalPages = 0;

  @Output() iobjPageChange = new EventEmitter<number>();

  get iarrPageNumbers(): number[] {
    return Array.from({ length: this.inumTotalPages }, (_, i) => i + 1);
  }

  goToPage(aNumPage: number): void {
    if (aNumPage < 1 || aNumPage > this.inumTotalPages || aNumPage === this.inumCurrentPage) return;
    this.iobjPageChange.emit(aNumPage);
  }
}