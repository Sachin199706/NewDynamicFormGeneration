import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { SubmissionService } from '../../core/services/submission';
import { FormService } from '../../core/services/form';
import { SubmissionFilter, SubmissionOverviewItem, SubmissionStats } from '../../core/models/rule.model';
import { FormListItem } from '../../core/models/form.model';
import { Pagination } from "../pagination/pagination";

@Component({
  selector: 'app-submissions-overview',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, Pagination],
  templateUrl: './submissions-overview.html',
  styleUrl: './submissions-overview.scss',
})
export class SubmissionsOverview implements OnInit {
  Math = Math;

  iarrSubmissions: SubmissionOverviewItem[] = [];
  iarrForms: FormListItem[] = [];
  iobjStats: SubmissionStats = { totalSubmissions: 0, unreadSubmissions: 0, readSubmissions: 0 };

  iobjFilter: SubmissionFilter = { page: 1, pageSize: 10 };
  inumTotalCount = 0;
  inumTotalPages = 0;

  constructor(private iobjSubmissionService: SubmissionService, private iobjFormService: FormService, private iobjRoute: ActivatedRoute) { }

  ngOnInit(): void {
    this.iobjFormService.getForms(1, 200).subscribe(res => this.iarrForms = res.items);

    const lStrFormIdParam = this.iobjRoute.snapshot.queryParamMap.get('formId');
    this.iobjFilter.formId = null;
    if (lStrFormIdParam) {
      this.iobjFilter.formId = Number(lStrFormIdParam);
    }
    this.iobjFilter.isRead = null;

    this.refreshStats(this.iobjFilter.formId);
    this.search();
  }

  get iarrPageNumbers(): number[] {
    return Array.from({ length: this.inumTotalPages }, (_, i) => i + 1);
  }

  private refreshStats(anumFormID:any): void {
    if (anumFormID == null) {
      this.iobjSubmissionService.getStats().subscribe(res => this.iobjStats = res);
      return;
    }
      this.iobjSubmissionService.getstatsById(anumFormID).subscribe(res => this.iobjStats = res);
  }

  onformChanges(anumFormID:any){
    this.refreshStats(anumFormID);
    this.loadPage();
  }

  search(): void {
    this.iobjFilter.page = 1;
    this.loadPage();
  }

  clear(): void {
    this.iobjFilter = { page: 1, pageSize: 10,formId:null,isRead:null};
    this.refreshStats(null);
    this.loadPage();

  }

  goToPage(aNumPage: number): void {
    if (aNumPage < 1 || aNumPage > this.inumTotalPages) return;
    this.iobjFilter.page = aNumPage;
    this.loadPage();
  }

  private loadPage(): void {
    this.iobjSubmissionService.getAllSubmissions(this.iobjFilter).subscribe(res => {
      this.iarrSubmissions = res.items;
      this.inumTotalCount = res.totalCount;
      this.inumTotalPages = res.totalPages;
    });
  }

  markAsRead(aObjRow: SubmissionOverviewItem): void {
    if (aObjRow.isRead) return;
    this.iobjSubmissionService.markAsRead(aObjRow.submissionId).subscribe(res => {
      if (res.success) {
        aObjRow.isRead = true;
        this.refreshStats(aObjRow.formId);
      }
    });
  }

  exportCsv(): void {
    const larrHeaders = ['Submission Code', 'Form Name', 'Version', 'Status', 'Submitted On'];
    const larrRows = this.iarrSubmissions.map(s => [
      s.submissionCode, s.formName, `v${s.versionNo}`, s.isRead ? 'Read' : 'Unread', s.submittedOn
    ]);

    const lstrCsv = [larrHeaders, ...larrRows]
      .map(row => row.map(cell => `"${String(cell).replace(/"/g, '""')}"`).join(','))
      .join('\n');

    const lobjBlob = new Blob([lstrCsv], { type: 'text/csv;charset=utf-8;' });
    const lstrUrl = URL.createObjectURL(lobjBlob);
    const lobjLink = document.createElement('a');
    lobjLink.href = lstrUrl;
    lobjLink.download = `submissions-page-${this.iobjFilter.page}.csv`;
    lobjLink.click();
    URL.revokeObjectURL(lstrUrl);
  }
}