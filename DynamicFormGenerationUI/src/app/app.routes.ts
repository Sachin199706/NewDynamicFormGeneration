import { Routes } from '@angular/router';

export const routes: Routes = [{ path: '', redirectTo: 'dashboard', pathMatch: 'full' },
{
    path: 'dashboard',
    loadComponent: () => import('./features/dashboard/dashboard').then(m => m.Dashboard)
},
{
    path: 'forms',
    loadComponent: () => import('./features/forms-list/forms-list').then(m => m.FormsList)
},
{
    path: 'forms/builder',
    loadComponent: () => import('./features/form-builder/form-builder').then(m => m.FormBuilder)
},
{
    path: 'forms/builder/:formId',
    loadComponent: () => import('./features/form-builder/form-builder').then(m => m.FormBuilder)
},
{
    path: 'forms/:formId/versions/:versionId/rules',
    loadComponent: () => import('./features/form-rules/form-rules').then(m => m.FormRules)
},
{
    path: 'forms/:formId/versions/:versionId/fill',
    loadComponent: () => import('./features/form-render/form-render').then(m => m.FormRender)
},
{
  path: 'submissions',
  loadComponent: () => import('./features/submissions-overview/submissions-overview').then(m => m.SubmissionsOverview)
},
{
    path: 'formtemplates',
    loadComponent: () => import('./features/form-template/form-template').then(m => m.FormTemplate)
},
{ path: '**', redirectTo: 'dashboard' }
];


