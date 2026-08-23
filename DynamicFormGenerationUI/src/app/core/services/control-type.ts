import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ControlType } from '../models/form.model';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class ControlTypeService {
  constructor(private http: HttpClient) { }

  getAll(): Observable<ControlType[]> {
    return this.http.get<ControlType[]>(`${environment.apiUrl}/control-types`);
  }

}
