type PaginationProps = {
  totalItems: number;
  page: number;
  onPageChange: (page: number) => void;
  itemLabel: string;
  hideSinglePage?: boolean;
};

export const PAGE_SIZE = 20;

export function Pagination({ totalItems, page, onPageChange, itemLabel, hideSinglePage = false }: PaginationProps) {
  const totalPages = Math.ceil(totalItems / PAGE_SIZE);
  if (totalItems === 0 || (hideSinglePage && totalPages <= 1)) return null;

  return (
    <div className="pagination">
      <div>
        Hiển thị <strong>{(page - 1) * PAGE_SIZE + 1}</strong> -{' '}
        <strong>{Math.min(page * PAGE_SIZE, totalItems)}</strong> trên tổng số{' '}
        <strong>{totalItems}</strong> {itemLabel}
      </div>
      <div className="flex gap-2">
        <button type="button" className="btn btn-secondary px-3 py-1.5 text-xs"
          disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
          Trang trước
        </button>
        <span className="flex items-center px-2 text-sm text-slate-600">Trang {page} / {totalPages}</span>
        <button type="button" className="btn btn-secondary px-3 py-1.5 text-xs"
          disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>
          Trang sau
        </button>
      </div>
    </div>
  );
}
