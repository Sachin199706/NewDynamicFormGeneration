import { Component } from '@angular/core';
import { FormListItem } from '../../core/models/form.model';
import { FormService } from '../../core/services/form';
import { RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, CommonModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard {
  iarrForms: FormListItem[] = [];
  inumTotal: number = 0;
  inumPublished: number = 0;
  inumDraft: number = 0;
  inumArchived: number = 0;

  constructor(private formService: FormService) { }

  ngOnInit(): void {
    this.formService.getForms(1, 50).subscribe(res => {
      this.iarrForms = res.items;
      this.inumTotal = res.totalCount;
      this.inumPublished = res.items.filter(f => f.status === 'Published').length;
      this.inumDraft = res.items.filter(f => f.status === 'Draft').length;
      this.inumArchived = res.items.filter(f => f.status === 'Archived').length;
    });
  }
}


