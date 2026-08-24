import { Component, OnInit } from '@angular/core';
import { SubmissionListItem } from '../../core/models/rule.model';
import { ActivatedRoute } from '@angular/router';
import { SubmissionService } from '../../core/services/submission';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-submissions',
  imports: [CommonModule],
  templateUrl: './submissions.html',
  styleUrl: './submissions.scss',
})
export class Submissions implements OnInit {
  submissions: SubmissionListItem[] = [];

  constructor(private route: ActivatedRoute, private submissionService: SubmissionService) { }

  ngOnInit(): void {
    const formId = Number(this.route.snapshot.paramMap.get('formId'));
    this.submissionService.getSubmissions(formId).subscribe(res => this.submissions = res.items);
  }


}
