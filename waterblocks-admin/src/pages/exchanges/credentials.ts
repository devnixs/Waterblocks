/**
 * Encrypts exchange credentials in the Fireblocks way: RSA-OAEP with SHA-256 over {"apiKey","secret","tenantId"},
 * with the credentials public key. The result is the Base64 creds value. The secret never leaves the browser in clear text.
 */
export async function encryptCredentials(
  publicKeyPem: string,
  apiKey: string,
  secret: string,
  tenantId: string,
): Promise<string> {
  if (!globalThis.crypto?.subtle) {
    throw new Error('This browser cannot encrypt the credentials here. Open the Admin UI over https or on localhost.');
  }

  const base64Key = publicKeyPem.replace(/-----(BEGIN|END) PUBLIC KEY-----|\s/g, '');
  const keyBytes = Uint8Array.from(atob(base64Key), (character) => character.charCodeAt(0));
  const key = await crypto.subtle.importKey('spki', keyBytes, { name: 'RSA-OAEP', hash: 'SHA-256' }, false, ['encrypt']);
  const payload = new TextEncoder().encode(JSON.stringify({ apiKey, secret, tenantId }));
  const encrypted = new Uint8Array(await crypto.subtle.encrypt({ name: 'RSA-OAEP' }, key, payload));

  let binary = '';
  encrypted.forEach((byte) => {
    binary += String.fromCharCode(byte);
  });
  return btoa(binary);
}
