import * as Dialog from '@radix-ui/react-dialog';

interface KeyboardShortcutsDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function KeyboardShortcutsDialog({ open, onOpenChange }: KeyboardShortcutsDialogProps) {
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 bg-black/70 z-[9998] backdrop-blur-sm" style={{ position: 'fixed', inset: 0, background: 'rgba(0, 0, 0, 0.7)', zIndex: 9998 }} />
        <Dialog.Content
          className="fixed top-1/2 left-1/2 p-8 max-w-2xl w-[90%] max-h-[80vh] overflow-auto z-[9999] bg-secondary border border-tertiary rounded-xl shadow-2xl animate-in zoom-in-95 duration-200"
          style={{
            position: 'fixed',
            top: '50%',
            left: '50%',
            transform: 'translate(-50%, -50%)',
            background: 'var(--bg-secondary)',
            border: '1px solid var(--bg-tertiary)',
            borderRadius: 'var(--radius-lg)',
            padding: 'var(--space-8)',
            maxWidth: '600px',
            width: '90%',
            maxHeight: '80vh',
            overflow: 'auto',
            zIndex: 9999,
          }}
        >
          <Dialog.Title className="text-2xl font-bold mb-6" style={{ fontSize: '1.5rem', fontWeight: 600, marginBottom: '1.5rem' }}>
            Keyboard Shortcuts
          </Dialog.Title>

          <div className="grid gap-6">
            <Section title="Global">
              <Shortcut keys={['1']} description="Navigate to Transactions" />
              <Shortcut keys={['2']} description="Navigate to Vaults" />
              <Shortcut keys={['3']} description="Navigate to Workspaces" />
              <Shortcut keys={['4']} description="Navigate to Assets" />
              <Shortcut keys={['/']} description="Focus search/filter input" />
              <Shortcut keys={['Esc']} description="Close panel or clear selection" />
              <Shortcut keys={['?']} description="Show this help dialog" />
            </Section>

            <Section title="List Navigation">
              <Shortcut keys={['j', '↓']} description="Move selection down" />
              <Shortcut keys={['k', '↑']} description="Move selection up" />
              <Shortcut keys={['Enter']} description="Open detail panel" />
              <Shortcut keys={['Space']} description="Toggle checkbox" />
              <Shortcut keys={['Ctrl/Cmd', 'A']} description="Select all" />
              <Shortcut keys={['Ctrl/Cmd', 'D']} description="Deselect all" />
            </Section>

            <Section title="Transaction Detail Panel">
              <Shortcut keys={['a']} description="Approve transaction" />
              <Shortcut keys={['s']} description="Sign transaction" />
              <Shortcut keys={['c']} description="Complete transaction" />
              <Shortcut keys={['f']} description="Fail transaction" />
              <Shortcut keys={['x']} description="Cancel transaction" />
              <Shortcut keys={['Ctrl/Cmd', 'C']} description="Copy transaction ID" />
            </Section>
          </div>

          <Dialog.Close asChild>
            <button
              className="btn btn-primary w-full mt-8"
              style={{ marginTop: '2rem', width: '100%' }}
            >
              Close
            </button>
          </Dialog.Close>

          <Dialog.Close
            className="absolute top-4 right-4 text-secondary hover:text-primary transition-colors text-2xl"
            style={{
              position: 'absolute',
              top: '1rem',
              right: '1rem',
              background: 'none',
              border: 'none',
              color: 'var(--text-secondary)',
              fontSize: '1.5rem',
              cursor: 'pointer',
              padding: '0.5rem',
            }}
            aria-label="Close"
          >
            ×
          </Dialog.Close>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div>
      <h3 className="text-sm font-bold text-muted uppercase tracking-wider mb-3" style={{ fontSize: '0.85rem', fontWeight: 600, marginBottom: '0.75rem', color: 'var(--text-secondary)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
        {title}
      </h3>
      <div className="grid gap-2">
        {children}
      </div>
    </div>
  );
}

function Shortcut({ keys, description }: { keys: string[]; description: string }) {
  return (
    <div className="flex justify-between items-center group">
      <span className="text-muted group-hover:text-primary transition-colors" style={{ color: 'var(--text-secondary)' }}>{description}</span>
      <div className="flex gap-1" style={{ display: 'flex', gap: '0.25rem' }}>
        {keys.map((key, i) => (
          <kbd
            key={i}
            className="bg-tertiary border border-tertiary rounded px-2 py-1 text-xs font-mono min-w-[24px] text-center"
            style={{
              background: 'var(--bg-tertiary)',
              border: '1px solid var(--bg-tertiary)',
              borderRadius: '4px',
              padding: '0.25rem 0.5rem',
              fontSize: '0.75rem',
              fontFamily: 'monospace',
              color: 'var(--text-primary)',
            }}
          >
            {key}
          </kbd>
        ))}
      </div>
    </div>
  );
}
