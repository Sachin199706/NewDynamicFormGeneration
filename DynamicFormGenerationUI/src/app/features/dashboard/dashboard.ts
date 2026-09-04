import { Component, OnInit } from '@angular/core';
import { FormListItem, FormVersionListItem, DashboardItems } from '../../core/models/form.model';
import { FormService } from '../../core/services/form';
import { RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, CommonModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard implements OnInit {
  iarrForms: FormListItem[] = [];
  inumTotal: number = 0;
  inumTotalVersions: number = 0;
  inumPublished: number = 0;
  inumDraft: number = 0;
  inumArchived: number = 0;
  inumSubmissions: number = 0;
  iarrDraftVersions: FormVersionListItem[] = [];
  

  constructor(private formService: FormService) { }

  ngOnInit(): void {
    this.formService.getForms(1, 50).subscribe(res => {
      this.iarrForms = res.items;
    });
    this.formService.getDashboardCount().subscribe(dashboardCounts => {
        this.iarrDraftVersions = dashboardCounts.recentForms;
        this.inumArchived = dashboardCounts.archivedForms;
        this.inumDraft = dashboardCounts.draftForms;
        this.inumPublished = dashboardCounts.publishedForms;
        this.inumTotal = dashboardCounts.totalForms;
    });  
}
}


