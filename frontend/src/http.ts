/** Shared JSON transport; credentials and provider clients stay on the server. */
export async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, init);
  if (!res.ok) {
    let message = `${res.status} ${res.statusText}`;
    try {
      const body = await res.json();
      message = body.title ?? body.error ?? message;
      if (Array.isArray(body.errors) && body.errors.length)
        message += ": " + body.errors.join("، ");
    } catch {
      /* non-JSON error body */
    }
    throw new Error(message);
  }
  return res.json() as Promise<T>;
}
