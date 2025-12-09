import React, { useEffect, useMemo, useState } from 'react'
import axios from 'axios'
import { api } from './apiClient'
import { Routes, Route, Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import Login from './Login'
import Register from './Register'
import Admin from './Admin'

const apiSource = import.meta.env.VITE_API_SOURCE || 'jdbc'
const JDBC_API_URL = import.meta.env.VITE_JDBC_API_URL || 'http://localhost:8080/api'
const ADO_API_URL = import.meta.env.VITE_ADO_API_URL || 'http://localhost:5001/api'
const ODBC_API_URL = import.meta.env.VITE_ODBC_API_URL || 'http://localhost:5002/api'

const SOURCE_LABEL = { jdbc: 'JDBC', ado: 'ADO.NET', odbc: 'ODBC' }

const Badge = ({ type }) => {
  const map = {
    verde: 'badge badge-salsa-verde',
    roja: 'badge badge-salsa-roja',
    mole: 'badge badge-salsa-mole'
  }
  return <span className={map[type] || 'badge'}>{type}</span>
}

function isAuthed() { return typeof localStorage !== 'undefined' && !!localStorage.getItem('token') }

export default function App() {
  // Do not send cookies; we use Bearer tokens
  axios.defaults.withCredentials = false
    // Ensure API client does not send cookies
    if (api && api.defaults) {
      api.defaults.withCredentials = false
    }
  const [source, setSource] = useState(apiSource)
  const baseUrl = useMemo(() => {
    if (source === 'ado') return ADO_API_URL
    if (source === 'odbc') return ODBC_API_URL
    return JDBC_API_URL
  }, [source])
  const [items, setItems] = useState([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [backendStatus, setBackendStatus] = useState('ok') // ok | down
  const location = useLocation()
  const navigate = useNavigate()

  const [filters, setFilters] = useState({ salsaType: '', protein: '', spiciness: '', includeInactive: false })
  const [page, setPage] = useState(1)
  const pageSize = 10
  const emptyForm = { name: '', salsaType: 'verde', protein: 'pollo', spiciness: 0, price: 0 }
  const [form, setForm] = useState(emptyForm)
  const [editingId, setEditingId] = useState(null)
  const [actionMsg, setActionMsg] = useState('')
  const [showModal, setShowModal] = useState(false)

  // Verify token by calling /me when app loads or source changes
  useEffect(() => {
    if (!isAuthed()) return
    const verify = async () => {
      try {
        await api.get('/me')
      } catch (e) {
        // if token invalid, force logout
        localStorage.removeItem('token')
        navigate('/login')
      }
    }
    verify()
  }, [source])

  useEffect(() => {
    if (location.pathname !== '/') return
    const load = async () => {
      setLoading(true); setError('')
      try {
        const params = { ...filters, page, pageSize }
        const res = await api.get('/chilaquiles', { params })
        setBackendStatus('ok')
        setItems(res.data)
      } catch (e) {
        setBackendStatus('down')
        setItems([])
        setError(`La conexión con ${SOURCE_LABEL[source]} no se está usando.`)
      } finally {
        setLoading(false)
      }
    }
    load()
  }, [baseUrl, filters, page, source, location.pathname])

  const submitCreate = async () => {
    setActionMsg('')
    try {
      await api.post('/chilaquiles', form)
      setForm(emptyForm)
      setEditingId(null)
      setShowModal(false)
      setActionMsg('Creado correctamente')
      const params = { ...filters, page, pageSize }
      const res = await api.get('/chilaquiles', { params })
      setItems(res.data)
    } catch (e) {
      setActionMsg('Error al crear el registro')
    }
  }

  const submitUpdate = async () => {
    setActionMsg('')
    try {
      await api.put(`/chilaquiles/${editingId}`, form)
      setForm(emptyForm)
      setEditingId(null)
      setShowModal(false)
      setActionMsg('Actualizado correctamente')
      const params = { ...filters, page, pageSize }
      const res = await api.get('/chilaquiles', { params })
      setItems(res.data)
    } catch (e) {
      setActionMsg('Error al actualizar el registro')
    }
  }

  const doDelete = async (id) => {
    setActionMsg('')
    try {
      await api.delete(`/chilaquiles/${id}`)
      setActionMsg('Baja lógica aplicada')
      const params = { ...filters, page, pageSize }
      const res = await api.get('/chilaquiles', { params })
      setItems(res.data)
    } catch (e) {
      setActionMsg('Error al aplicar baja lógica')
    }
  }

  const doRestore = async (id) => {
    setActionMsg('')
    try {
      await api.post(`/chilaquiles/${id}/restore`)
      setActionMsg('Alta lógica aplicada')
      const params = { ...filters, page, pageSize }
      const res = await api.get('/chilaquiles', { params })
      setItems(res.data)
    } catch (e) {
      setActionMsg('Error al aplicar alta lógica')
    }
  }

  return (
    <>
      {location.pathname !== '/login' && location.pathname !== '/register' && (
        <div className="header">
          <div className="header-inner">
            <div className="brand">
              <img className="brand-logo" src="https://imgs.search.brave.com/DLL6__zDVNQcr_m2mTk257fK5GD7Ji4QG50zDS-1NrM/rs:fit:860:0:0:0/g:ce/aHR0cHM6Ly9jZG4u/ZHJpYmJibGUuY29t/L3VzZXJ1cGxvYWQv/MzM1MzEyNzgvZmls/ZS9vcmlnaW5hbC02/OTUyZDg5ZWRmYmI3/ODQzMjYxY2Y0MGJk/MmJhY2EwYS5wbmc_/Zm9ybWF0PXdlYnAm/cmVzaXplPTQwMHgz/MDAmdmVydGljYWw9/Y2VudGVy  " alt="Logo" />
              <span className="brand-title">Chilaquiles</span>
            </div>
            <div className="nav">
              <Link className="link" to="/">Inicio</Link>
              <Link className="link" to="/login">Iniciar sesión</Link>
              <Link className="link" to="/register">Registro</Link>
              <Link className="link" to="/admin">Admin</Link>
              <a className="link" href="#" onClick={async (e) => { e.preventDefault(); try { await api.post('/auth/logout'); localStorage.removeItem('token'); } catch {} navigate('/login') }}>Cerrar sesión</a>
            </div>
          </div>
        </div>
      )}
      <div className="container">
      {location.pathname === '/' && (
      <header className="card">
        <div className="card-header">
          <div style={{ flex: 1 }}>
            <h1 className="title">Chilaquiles</h1>
            <p className="subtitle">Explora combinaciones de salsa, proteína y picor en un UI moderno.</p>
            {source === 'ado' && (
              <p className="subtitle" style={{ marginTop: 6 }}>
                Backend .NET ADO.NET
              </p>
            )}
            {source === 'odbc' && (
              <p className="subtitle" style={{ marginTop: 6 }}>
                Backend .NET ODBC
              </p>
            )}
            {source === 'jdbc' && (
              <p className="subtitle" style={{ marginTop: 6 }}>
                Backend Java JDBC
              </p>
            )}
            {backendStatus === 'down' && (
              <p className="subtitle" style={{ marginTop: 6, color: '#b1251b' }}>
                La conexión con {SOURCE_LABEL[source]} no se está usando.
              </p>
            )}
          </div>
          <img alt="Chilaquiles" src="https://imgs.search.brave.com/DLL6__zDVNQcr_m2mTk257fK5GD7Ji4QG50zDS-1NrM/rs:fit:860:0:0:0/g:ce/aHR0cHM6Ly9jZG4u/ZHJpYmJibGUuY29t/L3VzZXJ1cGxvYWQv/MzM1MzEyNzgvZmls/ZS9vcmlnaW5hbC02/OTUyZDg5ZWRmYmI3/ODQzMjYxY2Y0MGJk/MmJhY2EwYS5wbmc_/Zm9ybWF0PXdlYnAm/cmVzaXplPTQwMHgz/MDAmdmVydGljYWw9/Y2VudGVy" className="hero-img" />
        </div>
      </header>
      )}

      <Routes>
        <Route path="/" element={
          isAuthed() ? (
            <section className="card" style={{ marginTop: 16 }}>
        <div className="card-body">
          <div className="controls">
            <div className="control">
              <label className="label">Backend</label>
              <select className="select" value={source} onChange={e => setSource(e.target.value)}>
                <option value="jdbc">Java JDBC</option>
                <option value="ado">.NET ADO.NET</option>
                <option value="odbc">.NET ODBC</option>
              </select>
            </div>
            <div className="control">
              <label className="label">Salsa</label>
              <select className="select" value={filters.salsaType} onChange={e => setFilters(f => ({ ...f, salsaType: e.target.value }))}>
                <option value="">Todas</option>
                <option value="verde">Verde</option>
                <option value="roja">Roja</option>
                <option value="mole">Mole</option>
              </select>
            </div>
            <div className="control">
              <label className="label">Proteína</label>
              <select className="select" value={filters.protein} onChange={e => setFilters(f => ({ ...f, protein: e.target.value }))}>
                <option value="">Todas</option>
                <option value="pollo">Pollo</option>
                <option value="res">Res</option>
                <option value="huevo">Huevo</option>
                <option value="queso">Queso</option>
                <option value="sin-proteina">Sin proteína</option>
              </select>
            </div>
            <div className="control">
              <label className="label">Picor</label>
              <input className="input" type="number" min="0" max="5" value={filters.spiciness} onChange={e => setFilters(f => ({ ...f, spiciness: e.target.value }))} />
            </div>
            <div className="control">
              <label className="label">Incluir inactivos</label>
              <input type="checkbox" checked={filters.includeInactive} onChange={e => setFilters(f => ({ ...f, includeInactive: e.target.checked }))} />
            </div>
            <div className="actions">
              <button className="btn btn-secondary" onClick={() => { setFilters({ salsaType: '', protein: '', spiciness: '', includeInactive: false }); setPage(1); }}>Limpiar</button>
            </div>
          </div>
          <div className="actions" style={{ marginTop: 12 }}>
            <button className="btn btn-primary" onClick={() => { setEditingId(null); setForm(emptyForm); setShowModal(true) }}>Agregar</button>
          </div>
          {actionMsg && <p className="subtitle" style={{ marginTop: 8 }}>{actionMsg}</p>}
        </div>
            </section>
          ) : (<Navigate to="/login" replace />)
        } />
        <Route path="/login" element={<Login baseUrl={baseUrl} onLoggedIn={(path) => navigate(path)} />} />
        <Route path="/register" element={<Register baseUrl={baseUrl} onRegistered={() => navigate('/login')} />} />
        <Route path="/admin" element={<Admin baseUrl={baseUrl} />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>

      {location.pathname === '/' && (
      <section className="grid" style={{ marginTop: 18 }}>
        {loading && (
          <div style={{ gridColumn: '1/-1', textAlign: 'center', color: '#7a312b' }}>Cargando...</div>
        )}
        {error && (
          <div style={{ gridColumn: '1/-1', textAlign: 'center', color: '#b1251b' }}>{error}</div>
        )}
        {!loading && !error && items.map(x => (
          <article key={x.id} className="card card-item">
            <div className="card-body">
              <div className="item-top">
                <h3 className="item-title">{x.name}</h3>
                <span className="item-id">#{x.id}</span>
              </div>
              <div className="badges">
                <Badge type={x.salsaType} />
                <span className="badge badge-protein">{x.protein}</span>
                <span className="badge badge-spicy">Picor {x.spiciness}</span>
                <span className="badge badge-price">${x.price}</span>
                {x.isActive === false && <span className="badge badge-inactive">Inactivo</span>}
              </div>
              <p className="subtitle" style={{ marginTop: 10 }}>Creado: {String(x.createdAt)}</p>
              <div className="actions" style={{ marginTop: 8 }}>
                <button className="btn btn-secondary" onClick={() => { setEditingId(x.id); setForm({ name: x.name, salsaType: x.salsaType, protein: x.protein, spiciness: x.spiciness, price: x.price }); setShowModal(true) }}>Editar</button>
                {x.isActive === false ? (
                  <button className="btn btn-primary" onClick={() => doRestore(x.id)}>Restaurar</button>
                ) : (
                  <button className="btn btn-danger" onClick={() => doDelete(x.id)}>Eliminar</button>
                )}
              </div>
            </div>
          </article>
        ))}
      </section>
      )}

      {showModal && (
        <div className="modal-overlay" onClick={() => { setShowModal(false); }}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3 className="modal-title">{editingId ? 'Editar chilaquiles' : 'Agregar chilaquiles'}</h3>
              <button className="btn btn-secondary" onClick={() => { setShowModal(false); }}>Cerrar</button>
            </div>
            <div className="modal-body">
              <div className="controls" style={{ marginTop: 4 }}>
                <div className="control" style={{ gridColumn: '1 / span 3' }}>
                  <label className="label">Nombre</label>
                  <input className="input" value={form.name} onChange={e => setForm(f => ({ ...f, name: e.target.value }))} />
                </div>
                <div className="control">
                  <label className="label">Salsa</label>
                  <select className="select" value={form.salsaType} onChange={e => setForm(f => ({ ...f, salsaType: e.target.value }))}>
                    <option value="verde">Verde</option>
                    <option value="roja">Roja</option>
                    <option value="mole">Mole</option>
                  </select>
                </div>
                <div className="control">
                  <label className="label">Proteína</label>
                  <select className="select" value={form.protein} onChange={e => setForm(f => ({ ...f, protein: e.target.value }))}>
                    <option value="pollo">Pollo</option>
                    <option value="res">Res</option>
                    <option value="huevo">Huevo</option>
                    <option value="queso">Queso</option>
                    <option value="sin-proteina">Sin proteína</option>
                  </select>
                </div>
                <div className="control">
                  <label className="label">Picor</label>
                  <input className="input" type="number" min="0" max="5" value={form.spiciness} onChange={e => setForm(f => ({ ...f, spiciness: Number(e.target.value) }))} />
                </div>
                <div className="control">
                  <label className="label">Precio</label>
                  <input className="input" type="number" step="0.01" value={form.price} onChange={e => setForm(f => ({ ...f, price: Number(e.target.value) }))} />
                </div>
              </div>
            </div>
            <div className="modal-footer">
              {editingId ? (
                <>
                  <button className="btn btn-primary" onClick={submitUpdate}>Guardar</button>
                  <button className="btn btn-secondary" onClick={() => { setEditingId(null); setForm(emptyForm); setShowModal(false) }}>Cancelar</button>
                </>
              ) : (
                <>
                  <button className="btn btn-primary" onClick={submitCreate}>Crear</button>
                  <button className="btn btn-secondary" onClick={() => { setForm(emptyForm); setShowModal(false) }}>Cancelar</button>
                </>
              )}
            </div>
          </div>
        </div>
      )}

      {location.pathname === '/' && (
        <div className="footer">
          <button className="btn btn-secondary" onClick={() => setPage(p => Math.max(1, p - 1))}>Anterior</button>
          <span className="page">Página {page}</span>
          <button className="btn btn-primary" onClick={() => setPage(p => p + 1)}>Siguiente</button>
        </div>
      )}
      </div>
    </>
  )
}
