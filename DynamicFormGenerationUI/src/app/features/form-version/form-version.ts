import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { FormService } from '../../core/services/form';
import { FormVersionListItem } from '../../core/models/form.model';


@Component({
  selector: 'app-forms-version',
  imports: [RouterLink, CommonModule, FormsModule],
  templateUrl: './form-version.html',
  styleUrl: './form-version.scss',
})

export class FormsVersion implements OnInit {
  strTemplateName: string = '';
  iarrForms: FormVersionListItem[] = [];
  inumFormTemplateId = 0;
  strSearch = '';
  dtFromDate: string | null = null;
  dtToDate: string | null = null;
  strStatus = 'All';
  constructor(private iobjFormService: FormService, private route: ActivatedRoute) { 

  }
  ngOnInit():void
  {
      this.inumFormTemplateId = Number(this.route.snapshot.paramMap.get('inumFormTemplateId'));
      this.strTemplateName = String(this.route.snapshot.queryParamMap.get('tname'));
      this.search();
}

  search(): void {
    this.iobjFormService.getVersions(this.inumFormTemplateId, 1, 10, this.strSearch, this.dtFromDate, this.dtToDate, this.strStatus)
      .subscribe(res => {
        this.iarrForms = res.items;
      });
  }

  clearFilters(): void {
    this.strSearch = '';
    this.dtFromDate = null;
    this.dtToDate = null;
    this.strStatus = 'All';
    this.search();
  }
}

