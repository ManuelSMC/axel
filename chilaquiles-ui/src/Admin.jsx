import React, { useEffect, useState } from 'react'
import { api } from './apiClient'
import { Navigate } from 'react-router-dom'

export default function Admin({ baseUrl }) {
  const [me, setMe] = useState(null)
  const [users, setUsers] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [form, setForm] = useState({ fullName: '', username: '', password: '', role: 'user' })
  const [msg, setMsg] = useState('')
  const [includeInactive, setIncludeInactive] = useState(false)
  const [editing, setEditing] = useState(null)

  useEffect(() => {
    const load = async () => {
      try {
        const meRes = await api.get('/me')
        setMe(meRes.data)
        if (meRes.data.role !== 'admin') { setLoading(false); return }
        const res = await api.get('/admin/users', { params: { includeInactive } })
        setUsers(res.data)
      } catch (e) {
        setError('No autorizado o error al cargar usuarios')
      } finally { setLoading(false) }
    }
    load()
  }, [baseUrl, includeInactive])

  const createUser = async () => {
    setMsg('')
    try {
      await api.post('/admin/users', form)
      setMsg('Usuario creado correctamente')
      setForm({ fullName: '', username: '', password: '', role: 'user' })
      const res = await api.get('/admin/users', { params: { includeInactive } })
      setUsers(res.data)
    } catch (e) { setMsg('Error al crear usuario') }
  }

  const updateUser = async () => {
    if (!editing) return
    setMsg('')
    try {
      await api.put(`/admin/users/${editing.id}`, { role: editing.role, fullName: editing.fullName, username: editing.username })
      setMsg('Usuario actualizado correctamente')
      setEditing(null)
      const res = await api.get('/admin/users', { params: { includeInactive } })
      setUsers(res.data)
    } catch (e) { setMsg('Error al actualizar usuario') }
  }

  const deactivateUser = async (id) => {
    setMsg('')
    try {
      await api.delete(`/admin/users/${id}`)
      setMsg('Usuario desactivado')
      const res = await api.get('/admin/users', { params: { includeInactive } })
      setUsers(res.data)
    } catch (e) { setMsg('Error al desactivar usuario') }
  }

  const restoreUser = async (id) => {
    setMsg('')
    try {
      await api.post(`/admin/users/${id}/restore`)
      setMsg('Usuario restaurado')
      const res = await api.get('/admin/users', { params: { includeInactive } })
      setUsers(res.data)
    } catch (e) { setMsg('Error al restaurar usuario') }
  }

  if (!loading && me && me.role !== 'admin') return <Navigate to="/" replace />

  return (
    <div className="container">
      <div className="card" style={{ marginTop: 16 }}>
        <div className="card-body">
          <h2 className="title">Admin: Gestión de Usuarios</h2>
          <p className="subtitle">Crea usuarios y asigna roles (admin/user).</p>
          {error && <div className="alert alert-error">{error}</div>}
          <div className="controls" style={{ marginTop: 12 }}>
            <div className="control">
              <label className="label">Nombre completo</label>
              <input className="input" value={form.fullName} onChange={e => setForm(f => ({ ...f, fullName: e.target.value }))} />
            </div>
            <div className="control">
              <label className="label">Usuario</label>
              <input className="input" value={form.username} onChange={e => setForm(f => ({ ...f, username: e.target.value }))} />
            </div>
            <div className="control">
              <label className="label">Contraseña</label>
              <input className="input" type="password" value={form.password} onChange={e => setForm(f => ({ ...f, password: e.target.value }))} />
            </div>
            <div className="control">
              <label className="label">Rol</label>
              <select className="select" value={form.role} onChange={e => setForm(f => ({ ...f, role: e.target.value }))}>
                <option value="user">user</option>
                <option value="admin">admin</option>
              </select>
            </div>
            <div className="actions">
              <button className="btn btn-primary" onClick={createUser}>Crear usuario</button>
            </div>
          </div>
          <div className="actions" style={{ marginTop: 10 }}>
            <label className="label" style={{ marginRight: 8 }}>Incluir inactivos</label>
            <input type="checkbox" checked={includeInactive} onChange={e => setIncludeInactive(e.target.checked)} />
          </div>
          {msg && <div className={`alert ${msg.includes('Error') ? 'alert-error' : 'alert-success'}`} style={{ marginTop: 10 }}>{msg}</div>}
        </div>
      </div>

      <section className="grid" style={{ marginTop: 18 }}>
        {users.map(u => (
          <article key={u.id} className="card card-item">
            <div className="card-body">
              <div className="item-top">
                <h3 className="item-title">{u.fullName}</h3>
                <span className="item-id">#{u.id}</span>
              </div>
              <div className="badges">
                <span className="badge">{u.username}</span>
                <span className="badge">{u.role}</span>
                {u.isActive === false && <span className="badge badge-inactive">Inactivo</span>}
              </div>
              <div className="actions" style={{ marginTop: 8 }}>
                <button className="btn btn-secondary" onClick={() => setEditing({ ...u })}>Editar</button>
                {u.isActive === false ? (
                  <button className="btn btn-primary" onClick={() => restoreUser(u.id)}>Restaurar</button>
                ) : (
                  <button className="btn btn-danger" onClick={() => deactivateUser(u.id)}>Desactivar</button>
                )}
              </div>
            </div>
          </article>
        ))}
      </section>

      {editing && (
        <div className="modal-overlay" onClick={() => setEditing(null)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <div className="modal-header">
              <h3 className="modal-title">Editar usuario</h3>
              <button className="btn btn-secondary" onClick={() => setEditing(null)}>Cerrar</button>
            </div>
            <div className="modal-body">
              <div className="controls">
                <div className="control">
                  <label className="label">Nombre completo</label>
                  <input className="input" value={editing.fullName} onChange={e => setEditing(ed => ({ ...ed, fullName: e.target.value }))} />
                </div>
                <div className="control">
                  <label className="label">Usuario</label>
                  <input className="input" value={editing.username} onChange={e => setEditing(ed => ({ ...ed, username: e.target.value }))} />
                </div>
                <div className="control">
                  <label className="label">Rol</label>
                  <select className="select" value={editing.role} onChange={e => setEditing(ed => ({ ...ed, role: e.target.value }))}>
                    <option value="user">user</option>
                    <option value="admin">admin</option>
                  </select>
                </div>
              </div>
            </div>
            <div className="modal-footer">
              <button className="btn btn-primary" onClick={updateUser}>Guardar</button>
              <button className="btn btn-secondary" onClick={() => setEditing(null)}>Cancelar</button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
