import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormListItem, FormVersionListItem, DashboardItems } from '../../core/models/form.model';
import { FormService } from '../../core/services/form';
import { RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { Pagination } from '../pagination/pagination';

const DEFAULT_PAGE_SIZE = 10;
const PAGE_SIZE_OPTIONS: number[] = [10, 25, 50];
const SEARCH_DEBOUNCE_MS = 300;

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, CommonModule, FormsModule,Pagination],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard implements OnInit, OnDestroy {
  iarrForms: FormListItem[] = [];
  inumTotal: number = 0;
  inumTotalVersions: number = 0;
  inumPublished: number = 0;
  inumDraft: number = 0;
  inumArchived: number = 0;
  inumSubmissions: number = 0;

  // Versions table: search and pagination
  iarrVersions: FormVersionListItem[] = [];
  istrSearch: string = '';
  inumCurrentPage: number = 1;
  inumPageSize: number = DEFAULT_PAGE_SIZE;
  inumTotalCount: number = 0;
  inumTotalPages: number = 0;
  iarrPageSizeOptions: number[] = PAGE_SIZE_OPTIONS;
  iboolLoadingVersions: boolean = false;
  Math = Math;
  private iobjSearchTimer: ReturnType<typeof setTimeout> | undefined;
  private inumLoadRequestId = 0;

  constructor(private formService: FormService, private iobjToastr: ToastrService) { }

  ngOnInit(): void {
    this.formService.getForms(1, 50).subscribe(res => {
      this.iarrForms = res.items;
    });
    this.formService.getDashboardCount().subscribe(dashboardCounts => {
        this.inumArchived = dashboardCounts.archivedForms;
        this.inumDraft = dashboardCounts.draftForms;
        this.inumPublished = dashboardCounts.publishedForms;
        this.inumTotal = dashboardCounts.totalForms;
        this.inumTotalVersions = dashboardCounts.totalVersions;
    });
    this.loadVersions();
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
      this.loadVersions();
    }, SEARCH_DEBOUNCE_MS);
  }

  clearSearch(): void {
    this.clearSearchTimer();
    this.istrSearch = '';
    this.inumCurrentPage = 1;
    this.loadVersions();
  }

  onPageSizeChange(): void {
    this.clearSearchTimer();
    this.inumCurrentPage = 1;
    this.loadVersions();
  }

  goToPage(aNumPage: number): void {
    if (aNumPage < 1 || aNumPage > this.inumTotalPages || aNumPage === this.inumCurrentPage) {
      return;
    }

    this.clearSearchTimer();
    this.inumCurrentPage = aNumPage;
    this.loadVersions();
  }

  private loadVersions(): void {
    // A newer request makes any response still in flight stale, so it is ignored below.
    const lnumRequestId = ++this.inumLoadRequestId;
    this.iboolLoadingVersions = true;
    this.formService.getDashboardVersions(
      this.inumCurrentPage,
      this.inumPageSize,
      this.istrSearch.trim()
    ).subscribe({
      next: (res) => {
        if (lnumRequestId !== this.inumLoadRequestId) {
          return;
        }

        // The current page can fall off the end when rows are removed elsewhere.
        if (res.totalPages > 0 && this.inumCurrentPage > res.totalPages) {
          this.inumCurrentPage = res.totalPages;
          this.loadVersions();
          return;
        }

        this.inumTotalCount = res.totalCount;
        this.inumTotalPages = res.totalPages;
        if (res.totalPages === 0) {
          this.inumCurrentPage = 1;
        }
        this.iarrVersions = res.items;
        this.iboolLoadingVersions = false;
      },
      error: () => {
        if (lnumRequestId !== this.inumLoadRequestId) {
          return;
        }

        this.iarrVersions = [];
        this.inumTotalCount = 0;
        this.inumTotalPages = 0;
        this.iboolLoadingVersions = false;
        this.iobjToastr.error('Unable to load forms.', 'Error');
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