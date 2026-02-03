import { useState, useRef, useEffect, useMemo } from 'react';
import type { AdminVault } from '../types/admin';

type SearchableVaultSelectProps = {
  vaults: AdminVault[];
  selectedVaultId: string;
  onSelect: (vaultId: string) => void;
  placeholder?: string;
  assetId?: string;
};

export function SearchableVaultSelect({
  vaults,
  selectedVaultId,
  onSelect,
  placeholder = 'Search by name, ID, or address...',
  assetId,
}: SearchableVaultSelectProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const containerRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const selectedVault = vaults.find((v) => v.id === selectedVaultId);

  const filteredVaults = useMemo(() => {
    if (!searchQuery.trim()) {
      return vaults;
    }

    const query = searchQuery.toLowerCase().trim();
    return vaults.filter((vault) => {
      if (vault.name.toLowerCase().includes(query)) {
        return true;
      }
      if (vault.id.toLowerCase().includes(query)) {
        return true;
      }
      for (const wallet of vault.wallets) {
        if (assetId && wallet.assetId !== assetId) {
          continue;
        }
        for (const address of wallet.addresses) {
          if (address.addressValue.toLowerCase().includes(query)) {
            return true;
          }
        }
        if (wallet.depositAddress?.toLowerCase().includes(query)) {
          return true;
        }
      }
      return false;
    });
  }, [vaults, searchQuery, assetId]);

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setIsOpen(false);
        setSearchQuery('');
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const handleSelect = (vaultId: string) => {
    onSelect(vaultId);
    setSearchQuery('');
    setIsOpen(false);
  };

  const handleDisplayClick = () => {
    setIsOpen(true);
    setTimeout(() => inputRef.current?.focus(), 0);
  };

  const handleInputChange = (value: string) => {
    setSearchQuery(value);
    if (!isOpen) {
      setIsOpen(true);
    }
  };

  const getVaultAddressForAsset = (vault: AdminVault): string | undefined => {
    if (!assetId) return undefined;
    const wallet = vault.wallets.find((w) => w.assetId === assetId);
    return wallet?.depositAddress;
  };

  const showInput = isOpen || !selectedVault;

  return (
    <div className="searchable-select" ref={containerRef}>
      <div className="searchable-select-input-wrapper">
        {showInput ? (
          <input
            ref={inputRef}
            type="text"
            className="searchable-select-input"
            placeholder={placeholder}
            value={searchQuery}
            onChange={(e) => handleInputChange(e.target.value)}
            onFocus={() => setIsOpen(true)}
            autoFocus={isOpen && !!selectedVault}
          />
        ) : (
          <div className="searchable-select-display" onClick={handleDisplayClick}>
            <span className="searchable-select-display-name">{selectedVault.name}</span>
            <span className="searchable-select-display-id">({selectedVault.id.slice(0, 8)}...)</span>
          </div>
        )}
        <button
          type="button"
          className="searchable-select-clear"
          onClick={(e) => {
            e.stopPropagation();
            onSelect('');
            setSearchQuery('');
            setIsOpen(true);
            setTimeout(() => inputRef.current?.focus(), 0);
          }}
          style={{ visibility: selectedVaultId ? 'visible' : 'hidden' }}
        >
          &times;
        </button>
      </div>
      {isOpen && (
        <div className="searchable-select-dropdown">
          {filteredVaults.length === 0 ? (
            <div className="searchable-select-empty">
              No vaults found
            </div>
          ) : (
            filteredVaults.map((vault) => {
              const address = getVaultAddressForAsset(vault);
              return (
                <div
                  key={vault.id}
                  className={`searchable-select-option ${vault.id === selectedVaultId ? 'selected' : ''}`}
                  onClick={() => handleSelect(vault.id)}
                >
                  <div className="searchable-select-option-name">
                    {vault.name}
                  </div>
                  <div className="searchable-select-option-details">
                    <span className="searchable-select-option-id">
                      {vault.id.slice(0, 8)}...
                    </span>
                    {address && (
                      <span className="searchable-select-option-address">
                        {address.slice(0, 10)}...{address.slice(-6)}
                      </span>
                    )}
                  </div>
                </div>
              );
            })
          )}
        </div>
      )}
    </div>
  );
}
