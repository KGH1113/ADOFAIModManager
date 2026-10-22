export function AppBrand({ compact = false }: { compact?: boolean }) {
  return (
    <div className={`app-brand ${compact ? "app-brand-compact" : ""}`}>
      <img
        src="/assets/app-icon.png"
        alt=""
        width={compact ? 27 : 42}
        height={compact ? 27 : 42}
        draggable={false}
      />
      <div>
        <strong>ADOFAI</strong>
        <span>MOD MANAGER</span>
      </div>
    </div>
  );
}
