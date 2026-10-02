import { of } from 'rxjs';
import { Router } from '@angular/router';
import { FormListItem } from '../../core/models/form.model';
import { FormService } from '../../core/services/form';
import { ToastrService } from 'ngx-toastr';
import { FormTemplate } from './form-template';

describe('FormTemplate pagination', () => {
  const formItems: FormListItem[] = [];
  const pagedResult = {
    items: formItems,
    page: 1,
    pageSize: 10,
    totalCount: 24,
    totalPages: 3,
  };

  function createComponent() {
    const formService = {
      getForms: vi.fn().mockReturnValue(of(pagedResult)),
    } as unknown as FormService;
    const toastr = { error: vi.fn(), success: vi.fn() };
    const router = { navigate: vi.fn() };

    return {
      component: new FormTemplate(
        formService,
        toastr as unknown as ToastrService,
        router as unknown as Router
      ),
      formService,
    };
  }

  it('loads the first page and stores the API pagination metadata', () => {
    const { component, formService } = createComponent();

    component.ngOnInit();

    expect(formService.getForms).toHaveBeenCalledWith(1, 10, '', null, null);
    expect(component.iarrForms).toEqual(formItems);
    expect(component.inumTotalCount).toBe(24);
    expect(component.inumTotalPages).toBe(3);
  });

  it('loads the requested page and resets to page one when page size changes', () => {
    const { component, formService } = createComponent();
    component.inumTotalPages = 3;

    component.goToPage(2);
    expect(formService.getForms).toHaveBeenLastCalledWith(2, 10, '', null, null);

    component.inumPageSize = 25;
    component.onPageSizeChange();
    expect(formService.getForms).toHaveBeenLastCalledWith(1, 25, '', null, null);
    expect(component.inumCurrentPage).toBe(1);
  });

  it('debounces search criteria and resets pagination before loading', () => {
    vi.useFakeTimers();
    const { component, formService } = createComponent();
    component.inumCurrentPage = 3;
    component.strSearch = 'accounts';
    component.dtFromDate = '2026-01-01';

    component.onFilterChange();
    expect(component.inumCurrentPage).toBe(1);
    expect(formService.getForms).not.toHaveBeenCalled();

    vi.advanceTimersByTime(250);
    expect(formService.getForms).toHaveBeenCalledWith(1, 10, 'accounts', '2026-01-01', null);

    component.ngOnDestroy();
    vi.useRealTimers();
  });
});
