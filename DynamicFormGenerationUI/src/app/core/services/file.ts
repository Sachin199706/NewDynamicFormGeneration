import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface FileUploadResult {
  fileId: number;
  fileName: string;
}

@Injectable({ providedIn: 'root' })
export class FileService {
  constructor(private iobjHttp: HttpClient) {}

  upload(aNumSubmissionId: number, aNumControlId: number, aObjFile: File): Observable<FileUploadResult> {
    const lobjFormData = new FormData();
    lobjFormData.append('submissionId', aNumSubmissionId.toString());
    lobjFormData.append('controlId', aNumControlId.toString());
    lobjFormData.append('file', aObjFile);

    return this.iobjHttp.post<FileUploadResult>(`${environment.apiUrl}/files/upload`, lobjFormData);
  }
}