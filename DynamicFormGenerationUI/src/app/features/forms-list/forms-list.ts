import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { FormPublishHistoryItem } from '../../core/models/form.model';
import { FormService } from '../../core/services/form';
import { Pagination } from '../pagination/pagination';

const DEFAULT_PAGE_SIZE = 10;
const PAGE_SIZE_OPTIONS: number[] = [10, 25, 50];
const SEARCH_DEBOUNCE_MS = 300;

@Component({
  selector: 'app-forms-list',
  imports: [RouterLink, CommonModule, FormsModule,Pagination],
  templateUrl: './forms-list.html',
  styleUrl: './forms-list.scss',
})
export class FormsList implements OnInit, OnDestroy {
  iarrForms: FormPublishHistoryItem[] = [];
  strSearch = '';
  inumCurrentPage: number = 1;
  inumPageSize: number = DEFAULT_PAGE_SIZE;
  inumTotalCount: number = 0;
  inumTotalPages: number = 0;
  iarrPageSizeOptions: number[] = PAGE_SIZE_OPTIONS;
  iboolLoadingForms: boolean = false;
  // The search text the rows on screen were loaded with; decides which empty message to show.
  istrAppliedSearch: string = '';
  Math = Math;
  private iobjSearchTimer: ReturnType<typeof setTimeout> | undefined;
  private inumLoadRequestId = 0;

  constructor(private iobjFormService: FormService, private iobjToastr: ToastrService) { }

  ngOnInit(): void {
    this.loadPublishedForms();
  }

  ngOnDestroy(): void {
    this.clearSearchTimer();
    this.inumLoadRequestId++;
  }

  // Waits until typing pauses, then searches from the first page.
  onSearchChange(): void {
    this.inumCurrentPage = 1;
    this.inumLoadRequestId++;
    this.clearSearchTimer();
    this.iobjSearchTimer = setTimeout(() => {
      this.iobjSearchTimer = undefined;
      this.loadPublishedForms();
    }, SEARCH_DEBOUNCE_MS);
  }

  onPageSizeChange(): void {
    this.clearSearchTimer();
    this.inumCurrentPage = 1;
    this.loadPublishedForms();
  }

  goToPage(aNumPage: number): void {
    if (aNumPage < 1 || aNumPage > this.inumTotalPages || aNumPage === this.inumCurrentPage) {
      return;
    }

    this.clearSearchTimer();
    this.inumCurrentPage = aNumPage;
    this.loadPublishedForms();
  }

  private loadPublishedForms(): void {
    // A newer request makes any response still in flight stale, so it is ignored below.
    const lnumRequestId = ++this.inumLoadRequestId;
    const lstrSearch = this.strSearch.trim();
    this.iboolLoadingForms = true;
    this.iobjFormService.getPublishHistory(
      this.inumCurrentPage,
      this.inumPageSize,
      lstrSearch
    ).subscribe({
      next: (res) => {
        if (lnumRequestId !== this.inumLoadRequestId) {
          return;
        }

        // The current page can fall off the end when rows are removed elsewhere.
        if (res.totalPages > 0 && this.inumCurrentPage > res.totalPages) {
          this.inumCurrentPage = res.totalPages;
          this.loadPublishedForms();
          return;
        }

        this.inumTotalCount = res.totalCount;
        this.inumTotalPages = res.totalPages;
        if (res.totalPages === 0) {
          this.inumCurrentPage = 1;
        }
        this.iarrForms = res.items;
        this.istrAppliedSearch = lstrSearch;
        this.iboolLoadingForms = false;
      },
      error: () => {
        if (lnumRequestId !== this.inumLoadRequestId) {
          return;
        }

        this.iarrForms = [];
        this.inumTotalCount = 0;
        this.inumTotalPages = 0;
        this.istrAppliedSearch = lstrSearch;
        this.iboolLoadingForms = false;
        this.iobjToastr.error('Unable to load published forms.', 'Error');
      }
    });
  }

  private clearSearchTimer(): void {
    if (this.iobjSearchTimer !== undefined) {
      clearTimeout(this.iobjSearchTimer);
      this.iobjSearchTimer = undefined;
    }
  }
}