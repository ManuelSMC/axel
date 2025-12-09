import React, { useState } from 'react'
import axios from 'axios'
import { Link } from 'react-router-dom'

export default function Login({ baseUrl, onLoggedIn }) {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')

  const submit = async (e) => {
    e.preventDefault()
    setLoading(true); setError('')
    try {
      const res = await axios.post(`${baseUrl}/auth/login`, { username, password }, { withCredentials: true })
      const role = res?.data?.role || 'user'
      setSuccess('Inicio de sesión correcto. Redirigiendo…')
      setTimeout(() => {
        if (role === 'admin') onLoggedIn('/admin')
        else onLoggedIn('/')
      }, 600)
    } catch (err) {
      setError('Usuario o contraseña inválidos')
    } finally { setLoading(false) }
  }

  return (
    <div className="container">
      <div className="card" style={{ maxWidth: 520, margin: '40px auto' }}>
        <div className="card-body">
          <h2 className="title">Iniciar sesión</h2>
          <p className="subtitle">Accede para consultar y filtrar tus chilaquiles.</p>
          <form onSubmit={submit} style={{ display: 'grid', gap: 12, marginTop: 12 }}>
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
              <button className="btn btn-primary" type="submit" disabled={loading}>{loading ? 'Entrando...' : 'Entrar'}</button>
              <Link className="btn btn-secondary" to="/register">Crear cuenta</Link>
            </div>
          </form>
        </div>
      </div>
    </div>
  )
}
