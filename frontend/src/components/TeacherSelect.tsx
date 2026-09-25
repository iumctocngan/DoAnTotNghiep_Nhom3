import React, { useState } from 'react';
import type { StaffResponse } from '../types/staff';

interface TeacherSelectProps {
  id: string;
  teachers: StaffResponse[];
  value: string;
  search: string;
  isLoading: boolean;
  isSearching: boolean;
  disabled?: boolean;
  onSearchChange: (value: string) => void;
  onChange: (teacherId: string) => void;
}

export const TeacherSelect: React.FC<TeacherSelectProps> = ({
  id,
  teachers,
  value,
  search,
  isLoading,
  isSearching,
  disabled = false,
  onSearchChange,
  onChange,
}) => {
  const [isOpen, setIsOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);
  const selectedTeacher = teachers.find((teacher) => teacher.id === value);
  const inputValue = selectedTeacher
    ? `${selectedTeacher.fullName} (${selectedTeacher.employeeCode})`
    : search;

  const selectTeacher = (teacher: StaffResponse) => {
    onChange(teacher.id);
    setIsOpen(false);
  };

  return (
    <div className="teacher-select" onBlur={(event) => {
      if (!event.currentTarget.contains(event.relatedTarget as Node | null)) setIsOpen(false);
    }}>
      <input
        id={id}
        type="text"
        role="combobox"
        aria-label="Giáo viên"
        aria-autocomplete="list"
        aria-expanded={isOpen}
        aria-controls={`${id}-options`}
        aria-activedescendant={isOpen && !isLoading && !isSearching && teachers[activeIndex] ? `${id}-option-${teachers[activeIndex].id}` : undefined}
        autoComplete="off"
        placeholder="Tìm giáo viên"
        value={inputValue}
        disabled={disabled}
        onFocus={() => setIsOpen(true)}
        onChange={(event) => {
          onChange('');
          onSearchChange(event.target.value);
          setActiveIndex(-1);
          setIsOpen(true);
        }}
        onKeyDown={(event) => {
          if (event.key === 'ArrowDown') {
            event.preventDefault();
            setIsOpen(true);
            setActiveIndex((index) => Math.min(index + 1, teachers.length - 1));
          } else if (event.key === 'ArrowUp') {
            event.preventDefault();
            setActiveIndex((index) => Math.max(index - 1, 0));
          } else if (event.key === 'Enter' && isOpen && !isLoading && !isSearching && teachers[activeIndex]) {
            event.preventDefault();
            selectTeacher(teachers[activeIndex]);
          } else if (event.key === 'Escape') {
            setIsOpen(false);
          }
        }}
      />
      <button
        type="button"
        tabIndex={-1}
        className="absolute right-0 top-0 bottom-0 px-3 flex items-center justify-center text-slate-400 hover:text-slate-600 cursor-pointer border-0 bg-transparent transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
        onMouseDown={(e) => e.preventDefault()}
        onClick={() => {
          if (!disabled) {
            setIsOpen((prev) => !prev);
          }
        }}
        disabled={disabled}
        aria-label={isOpen ? 'Đóng danh sách giáo viên' : 'Mở danh sách giáo viên'}
        title={isOpen ? 'Đóng danh sách' : 'Mở danh sách giáo viên'}
      >
        <svg
          className={`w-4 h-4 transition-transform duration-200 ${isOpen ? 'rotate-180' : ''}`}
          viewBox="0 0 20 20"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          <polyline points="6 9 12 15 18 9" />
        </svg>
      </button>
      {isOpen && !disabled && (
        <div className="teacher-select-options" id={`${id}-options`} role="listbox">
          {isLoading || (isSearching && !teachers.length) ? (
            <div className="teacher-select-message" role="status">Đang tìm giáo viên…</div>
          ) : teachers.length ? teachers.map((teacher, index) => (
            <div
              key={teacher.id}
              id={`${id}-option-${teacher.id}`}
              className={`teacher-select-option${index === activeIndex ? ' is-active' : ''}${isSearching ? ' is-stale' : ''}`}
              role="option"
              aria-selected={teacher.id === value}
              aria-disabled={isSearching}
              onMouseDown={(event) => event.preventDefault()}
              onMouseEnter={() => setActiveIndex(index)}
              onClick={() => { if (!isSearching) selectTeacher(teacher); }}
            >
              {teacher.fullName} <span>({teacher.employeeCode})</span>
            </div>
          )) : (
            <div className="teacher-select-message">Không tìm thấy giáo viên.</div>
          )}
        </div>
      )}
    </div>
  );
};
