import * as Dialog from '@radix-ui/react-dialog';

interface KeyboardShortcutsDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function KeyboardShortcutsDialog({ open, onOpenChange }: KeyboardShortcutsDialogProps) {
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay
          style={{
            position: 'fixed',
            inset: 0,
            background: 'rgba(0, 0, 0, 0.7)',
            zIndex: 9998,
          }}
        />
        <Dialog.Content
          style={{
            position: 'fixed',
            top: '50%',
            left: '50%',
            transform: 'translate(-50%, -50%)',
            background: '#1a1a1a',
            border: '1px solid #333',
            borderRadius: '12px',
            padding: '2rem',
            maxWidth: '600px',
            width: '90%',
            maxHeight: '80vh',
            overflow: 'auto',
            zIndex: 9999,
          }}
        >
          <Dialog.Title style={{ fontSize: '1.5rem', fontWeight: 600, marginBottom: '1.5rem' }}>
            Keyboard Shortcuts
          </Dialog.Title>

          <div style={{ display: 'grid', gap: '1.5rem' }}>
            <Section title="Global">
              <Shortcut keys={['1']} description="Navigate to Transactions" />
              <Shortcut keys={['2']} description="Navigate to Vaults" />
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
              className="btn btn-primary"
              style={{ marginTop: '2rem', width: '100%' }}
            >
              Close
            </button>
          </Dialog.Close>

          <Dialog.Close
            style={{
              position: 'absolute',
              top: '1rem',
              right: '1rem',
              background: 'none',
              border: 'none',
              color: '#888',
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
      <h3 style={{ fontSize: '1rem', fontWeight: 600, marginBottom: '0.75rem', color: '#aaa' }}>
        {title}
      </h3>
      <div style={{ display: 'grid', gap: '0.5rem' }}>
        {children}
      </div>
    </div>
  );
}

function Shortcut({ keys, description }: { keys: string[]; description: string }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
      <span style={{ color: '#ccc' }}>{description}</span>
      <div style={{ display: 'flex', gap: '0.25rem' }}>
        {keys.map((key, i) => (
          <kbd
            key={i}
            style={{
              background: '#252525',
              border: '1px solid #444',
              borderRadius: '4px',
              padding: '0.25rem 0.5rem',
              fontSize: '0.75rem',
              fontFamily: 'monospace',
              color: '#fff',
            }}
          >
            {key}
          </kbd>
        ))}
      </div>
    </div>
  );
}
