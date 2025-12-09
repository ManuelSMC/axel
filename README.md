# Proyecto Chilaquiles

Este workspace ahora está dividido en TRES backends separados (uno por tecnología de conexión) para que puedas ejecutar en laptops distintas el mismo frontend y la misma BD (mismo esquema):

- `chilaquiles-java` (Spring Boot): JDBC → MySQL.
- `chilaquiles-dotnet-ado` (ASP.NET Core .NET 8): ADO.NET → MySQL.
- `chilaquiles-dotnet-odbc` (ASP.NET Core .NET 8): ODBC → MySQL (via DSN).
- `chilaquiles-ui` (React + Vite): Frontend con selector JDBC / ADO.NET / ODBC.

## Esquema de Base de Datos
Tabla: `chilaquiles`

Campos:
- `id` INT PK AUTO_INCREMENT
- `name` VARCHAR(100)
- `salsaType` ENUM('verde','roja','mole')
- `protein` ENUM('pollo','res','huevo','queso','sin-proteina')
- `spiciness` TINYINT (0-5)
- `price` DECIMAL(10,2)
- `createdAt` DATETIME
- `is_active` TINYINT(1) DEFAULT 1  
  (alta/baja lógica)

Tabla: `users`

Campos:
- `id` INT PK AUTO_INCREMENT
- `full_name` VARCHAR(120)
- `username` VARCHAR(60) UNIQUE
- `password_hash` VARCHAR(128)  
  (usa SHA-256 con salt o bcrypt; evita guardar contraseñas en claro)

Seed opcional:
```sql
INSERT INTO users(full_name, username, password_hash)
VALUES ('Usuario Demo', 'demo', SHA2(CONCAT('salt:', 'Demo.123'), 256));
```

## Variables de entorno
Copia `.env.example` a `.env` en la raíz del frontend y ajusta URLs (puertos recomendados):

- UI: `VITE_API_SOURCE=jdbc|ado|odbc`, `VITE_JDBC_API_URL=http://localhost:8080/api`, `VITE_ADO_API_URL=http://localhost:5001/api`, `VITE_ODBC_API_URL=http://localhost:5002/api`.
- Java (JDBC): `JAVA_MYSQL_*` para conexión local a MySQL.
- .NET ADO.NET: `MYSQL_*` (host, puerto, base, user, password).
- .NET ODBC: `ODBC_DSN` (por defecto `AXEL_DSN`).

## Comandos rápidos (PowerShell)

### MySQL local y datos demo
```powershell
mysql -u root -p -e "CREATE DATABASE IF NOT EXISTS axel; USE axel; \
CREATE TABLE IF NOT EXISTS chilaquiles(\
  id INT PRIMARY KEY AUTO_INCREMENT, \
  name VARCHAR(100), \
  salsaType ENUM('verde','roja','mole'), \
  protein ENUM('pollo','res','huevo','queso','sin-proteina'), \
  spiciness TINYINT, \
  price DECIMAL(10,2), \
  createdAt DATETIME, \
  is_active TINYINT(1) DEFAULT 1\
); \
INSERT INTO chilaquiles(name,salsaType,protein,spiciness,price,createdAt) VALUES \
('Clásicos Verdes','verde','pollo',3,95.00,NOW()), \
('Rojos con Queso','roja','queso',2,85.00,NOW()), \
('Mole con Huevo','mole','huevo',1,99.00,NOW());"
```

### ODBC DSN (Windows)
- Instala "MySQL ODBC 8.0".
- ODBC Data Sources (64-bit) → System DSN → Add → "MySQL ODBC 8.0" → Nombre `AXEL_DSN`.

### Backend .NET 8 (separados)
- ADO.NET
```powershell
cd c:\Users\manue\Documents\axel\chilaquiles-dotnet-ado
dotnet restore; dotnet run --urls http://localhost:5001
```
- ODBC (requiere DSN `AXEL_DSN`)
```powershell
cd c:\Users\manue\Documents\axel\chilaquiles-dotnet-odbc
$env:ODBC_DSN="AXEL_DSN"; dotnet restore; dotnet run --urls http://localhost:5002
```

### Backend Java (Spring Boot)
Requiere JDK y Maven:
```powershell
cd c:\Users\manue\Documents\axel
# Usa Spring Initializr (GUI o comando) con: 
# - Spring Web, Spring JDBC, MySQL Driver
# - Group: com.axel, Name: chilaquiles-java
# Luego:
cd chilaquiles-java
mvn spring-boot:run
```

### Frontend React (Vite)
```powershell
cd c:\Users\manue\Documents\axel
cd chilaquiles-ui
npm install
npm install axios
copy ..\.env.example .env  # o crea tu .env
npm run dev
```

## Escenarios por laptop
- Laptop A: BD local + UI + Backend JDBC (Java) en `8080`.
- Laptop B: BD local + UI + Backend ADO.NET (.NET) en `5001`.
- Laptop C: BD local + UI + Backend ODBC (.NET) en `5002`.

La BD debe tener el mismo nombre, tabla y campos en cada laptop (no es la misma instancia, pero el mismo esquema/seed).

## Endpoints (Java JDBC)
- `GET /api/chilaquiles?salsaType=&protein=&spiciness=&includeInactive=false&page=1&pageSize=20`
- `GET /api/chilaquiles/{id}`
- `POST /api/chilaquiles` body `{ name, salsaType, protein, spiciness, price }`
- `PUT /api/chilaquiles/{id}` body `{ name, salsaType, protein, spiciness, price }`
- `DELETE /api/chilaquiles/{id}` (baja lógica: `is_active=0`)
- `POST /api/chilaquiles/{id}/restore` (alta lógica: `is_active=1`)

## Siguientes pasos
1. Generar los proyectos (Java/.NET/React) con los comandos anteriores.
2. Implementar repositorios y controladores con filtros y paginación.
3. Ajustar `.env` y probar en local.
