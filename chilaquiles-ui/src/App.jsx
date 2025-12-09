import React, { useEffect, useMemo, useState } from 'react'
import axios from 'axios'

const apiSource = import.meta.env.VITE_API_SOURCE || 'java'
const JAVA_API_URL = import.meta.env.VITE_JAVA_API_URL || 'http://localhost:8080/api'
const DOTNET_API_URL = import.meta.env.VITE_DOTNET_API_URL || 'http://localhost:5000/api'

const Badge = ({ type }) => {
  const map = {
    verde: 'badge badge-salsa-verde',
    roja: 'badge badge-salsa-roja',
    mole: 'badge badge-salsa-mole'
  }
  return <span className={map[type] || 'badge'}>{type}</span>
}

export default function App() {
  const [source, setSource] = useState(apiSource)
  const baseUrl = useMemo(() => source === 'dotnet' ? DOTNET_API_URL : JAVA_API_URL, [source])
  const [items, setItems] = useState([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [backendInfo, setBackendInfo] = useState({ driver: '' })

  const [filters, setFilters] = useState({ salsaType: '', protein: '', spiciness: '' })
  const [page, setPage] = useState(1)
  const pageSize = 10

  useEffect(() => {
    const load = async () => {
      setLoading(true); setError('')
      try {
        // fetch backend health/driver
        const health = await axios.get(`${baseUrl}/health`)
        setBackendInfo({ driver: health.data?.driver || '' })
        const params = { ...filters, page, pageSize }
        const res = await axios.get(`${baseUrl}/chilaquiles`, { params })
        setItems(res.data)
      } catch (e) {
        setError(e.message)
      } finally {
        setLoading(false)
      }
    }
    load()
  }, [baseUrl, filters, page])

  return (
    <div className="container">
      <header className="card">
        <div className="card-header">
          <div style={{ flex: 1 }}>
            <h1 className="title">Chilaquiles</h1>
            <p className="subtitle">Explora combinaciones de salsa, proteína y picor en un UI moderno.</p>
            {source === 'dotnet' && (
              <p className="subtitle" style={{ marginTop: 6 }}>
                Backend .NET: {backendInfo.driver === 'odbc' ? 'ODBC' : 'ADO.NET'}
              </p>
            )}
            {source === 'java' && (
              <p className="subtitle" style={{ marginTop: 6 }}>
                Backend Java: JDBC
              </p>
            )}
          </div>
          <img alt="Chilaquiles" src="https://imgs.search.brave.com/DLL6__zDVNQcr_m2mTk257fK5GD7Ji4QG50zDS-1NrM/rs:fit:860:0:0:0/g:ce/aHR0cHM6Ly9jZG4u/ZHJpYmJibGUuY29t/L3VzZXJ1cGxvYWQv/MzM1MzEyNzgvZmls/ZS9vcmlnaW5hbC02/OTUyZDg5ZWRmYmI3/ODQzMjYxY2Y0MGJk/MmJhY2EwYS5wbmc_/Zm9ybWF0PXdlYnAm/cmVzaXplPTQwMHgz/MDAmdmVydGljYWw9/Y2VudGVy" className="hero-img" />
        </div>
      </header>

      <section className="card" style={{ marginTop: 16 }}>
        <div className="card-body">
          <div className="controls">
            <div className="control">
              <label className="label">Backend</label>
              <select className="select" value={source} onChange={e => setSource(e.target.value)}>
                <option value="java">Java</option>
                <option value="dotnet">.NET</option>
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
            <div className="actions">
              <button className="btn btn-primary" onClick={() => setPage(1)}>Aplicar</button>
              <button className="btn btn-secondary" onClick={() => { setFilters({ salsaType: '', protein: '', spiciness: '' }); setPage(1); }}>Limpiar</button>
            </div>
          </div>
        </div>
      </section>

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
              </div>
              <p className="subtitle" style={{ marginTop: 10 }}>Creado: {String(x.createdAt)}</p>
            </div>
          </article>
        ))}
      </section>

      <div className="footer">
        <button className="btn btn-secondary" onClick={() => setPage(p => Math.max(1, p - 1))}>Anterior</button>
        <span className="page">Página {page}</span>
        <button className="btn btn-primary" onClick={() => setPage(p => p + 1)}>Siguiente</button>
      </div>
    </div>
  )
}
