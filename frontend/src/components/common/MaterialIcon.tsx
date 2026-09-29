import { materialSymbols } from '../../assets/icons/materialSymbols';

type MaterialIconName = keyof typeof materialSymbols;

export function MaterialIcon({ name }: { name: MaterialIconName }) {
  const icon = materialSymbols[name];
  return (
    <svg className="material-icon" viewBox={icon.viewBox} fill="currentColor" aria-hidden="true" focusable="false">
      {icon.paths.map((path, index) => <path key={index} d={path} />)}
    </svg>
  );
}
