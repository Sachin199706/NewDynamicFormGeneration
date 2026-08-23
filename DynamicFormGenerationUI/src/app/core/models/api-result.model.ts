export interface PagedResult<T> {
    items: T[];
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
}


export interface ApiResult<T> {
    success: boolean;
    message?: string;
    data?: T;
    errors: string[];
}