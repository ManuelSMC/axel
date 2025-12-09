import axios from 'axios';

// Base URLs from Vite env
const SOURCE = import.meta.env.VITE_API_SOURCE || 'jdbc';
const BASES = {
  jdbc: import.meta.env.VITE_JDBC_API_URL || 'http://localhost:8080/api',
  ado: import.meta.env.VITE_ADO_API_URL || 'http://localhost:5001/api',
  odbc: import.meta.env.VITE_ODBC_API_URL || 'http://localhost:5002/api',
};

export const api = axios.create({
  baseURL: BASES[SOURCE],
  withCredentials: false,
});

// Attach Authorization: Bearer <token> if present
api.interceptors.request.use((config) => {
  const token = typeof localStorage !== 'undefined' ? localStorage.getItem('token') : null;
  if (token) {
    config.headers = config.headers || {};
    config.headers['Authorization'] = `Bearer ${token}`;
  }
  config.withCredentials = false;
  // Debug: log whether Authorization is being attached
  if (typeof console !== 'undefined') {
    console.debug('[api] Request', {
      url: (config.baseURL || '') + (config.url || ''),
      hasToken: !!token,
      authHeader: config.headers && config.headers['Authorization']
    });
  }
  return config;
});

export function setToken(token) {
  if (token) localStorage.setItem('token', token);
  else localStorage.removeItem('token');
}

export function currentBase() {
  return BASES[SOURCE];
}
