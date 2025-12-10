import React, { useState } from 'react'
import { api } from './apiClient'
import { Link } from 'react-router-dom'

export default function Register({ baseUrl, onRegistered }) {
  const [fullName, setFullName] = useState('')
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')

  const submit = async (e) => {
    e.preventDefault()
    setLoading(true); setError('')
    try {
      await api.post('/auth/register', { fullName, username, password })
      setSuccess('Registro completado correctamente. Ahora puedes iniciar sesión.')
      setTimeout(() => onRegistered(), 800)
    } catch (err) {
      setError('No se pudo registrar, intenta con otro usuario.')
    } finally { setLoading(false) }
  }

  return (
    <div className="container">
      <div className="card" style={{ maxWidth: 520, margin: '40px auto' }}>
        <div className="card-body">
          <h2 className="title">Crear cuenta</h2>
          <p className="subtitle">Regístrate con tu nombre y una contraseña segura.</p>
          <form onSubmit={submit} style={{ display: 'grid', gap: 12, marginTop: 12 }}>
            <div className="control">
              <label className="label">Nombre completo</label>
              <input className="input" value={fullName} onChange={e => setFullName(e.target.value)} required />
            </div>
            <div className="control">
              <label className="label">Nombre de usuario</label>
              <input className="input" value={username} onChange={e => setUsername(e.target.value)} required />
            </div>
            <div className="control">
              <label className="label">Contraseña</label>
              <input className="input" type="password" value={password} onChange={e => setPassword(e.target.value)} required />
            </div>
            {error && <div className="alert alert-error">{error}</div>}
            {success && <div className="alert alert-success">{success}</div>}
            <div className="actions" style={{ justifyContent: 'space-between' }}>
              <button className="btn btn-primary" type="submit" disabled={loading}>{loading ? 'Creando...' : 'Crear cuenta'}</button>
              <Link className="btn btn-secondary" to="/login">Ya tengo cuenta</Link>
            </div>
          </form>
        </div>
      </div>
    </div>
  )
}
