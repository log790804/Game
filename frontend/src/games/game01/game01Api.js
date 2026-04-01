const rawBaseUrl = import.meta.env.VITE_API_BASE_URL ?? '/api'
const baseUrl = rawBaseUrl.endsWith('/') ? rawBaseUrl.slice(0, -1) : rawBaseUrl

async function request(path, options = {}) {
  const response = await fetch(`${baseUrl}/game01${path}`, {
    headers: {
      'Content-Type': 'application/json',
      ...(options.headers ?? {})
    },
    ...options
  })

  if (!response.ok) {
    throw new Error(`Game01 API error: ${response.status}`)
  }

  return response.json()
}

export function fetchGame01Store() {
  return request('/')
}

export function saveGame01State(state) {
  return request('/state', {
    method: 'PUT',
    body: JSON.stringify(state)
  })
}

export function resetGame01(requestBody) {
  return request('/reset', {
    method: 'POST',
    body: JSON.stringify(requestBody)
  })
}

export function appendGame01Record(record) {
  return request('/records', {
    method: 'POST',
    body: JSON.stringify(record)
  })
}

export function clearGame01Records() {
  return request('/records', {
    method: 'DELETE'
  })
}
