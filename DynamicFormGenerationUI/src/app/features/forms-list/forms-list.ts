import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FormPublishHistoryItem } from '../../core/models/form.model';
import { FormService } from '../../core/services/form';

@Component({
  selector: 'app-forms-list',
  imports: [RouterLink, CommonModule, FormsModule],
  templateUrl: './forms-list.html',
  styleUrl: './forms-list.scss',
})
export class FormsList implements OnInit {
  iarrForms: FormPublishHistoryItem[] = [];
  strSearch = '';

  constructor(private iobjFormService: FormService) { }

  ngOnInit(): void {
    this.iobjFormService.getPublishHistory().subscribe(res => this.iarrForms = res);
  }

  get filteredForms(): FormPublishHistoryItem[] {
    const search = this.strSearch.trim().toLowerCase();

    if (!search) {
      return this.iarrForms;
    }

    return this.iarrForms.filter(form =>
      form.formName.toLowerCase().includes(search) ||
      form.versionDescription?.toLowerCase().includes(search) ||
      form.versionNo.toString().includes(search)
    );
  }
}