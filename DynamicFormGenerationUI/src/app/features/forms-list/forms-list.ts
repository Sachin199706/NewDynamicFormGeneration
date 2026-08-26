import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormPublishHistoryItem } from '../../core/models/form.model';
import { FormService } from '../../core/services/form';

@Component({
  selector: 'app-forms-list',
  imports: [RouterLink, CommonModule],
  templateUrl: './forms-list.html',
  styleUrl: './forms-list.scss',
})
export class FormsList implements OnInit {
  iarrForms: FormPublishHistoryItem[] = [];

  constructor(private iobjFormService: FormService) { }

  ngOnInit(): void {
    this.iobjFormService.getPublishHistory().subscribe(res => this.iarrForms = res);
  }
}