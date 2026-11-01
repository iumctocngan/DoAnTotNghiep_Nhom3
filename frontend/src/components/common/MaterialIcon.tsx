import { materialSymbols } from '../../assets/icons/materialSymbols';

type MaterialIconName = keyof typeof materialSymbols;

export function MaterialIcon({ name, className = '' }: { name: MaterialIconName; className?: string }) {
  const icon = materialSymbols[name];
  return (
    <svg className={`material-icon ${className}`.trim()} viewBox={icon.viewBox} fill="currentColor" aria-hidden="true" focusable="false">
      {icon.paths.map((path, index) => <path key={index} d={path} />)}
    </svg>
  );
}
