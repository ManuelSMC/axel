# Proyecto Chilaquiles

Este workspace contiene tres aplicaciones:

- `chilaquiles-java` (Spring Boot): API REST con JDBC a MySQL.
- `chilaquiles-dotnet` (ASP.NET Core .NET 8): API REST con ADO.NET (MySqlConnector) y opción ODBC.
- `chilaquiles-ui` (React + Vite): Frontend con tabla, filtros y detalle, con toggle para consultar Java o .NET.

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

## Variables de entorno
Crea `.env` en cada proyecto con:

- `MYSQL_HOST`, `MYSQL_PORT`, `MYSQL_DATABASE`, `MYSQL_USER`, `MYSQL_PASSWORD`
- Para .NET: `DB_DRIVER=ado|odbc` y si usas ODBC: DSN `AXEL_DSN`
- Para UI: `VITE_API_SOURCE=java|dotnet`, `VITE_JAVA_API_URL`, `VITE_DOTNET_API_URL`

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
  createdAt DATETIME\
); \
INSERT INTO chilaquiles(name,salsaType,protein,spiciness,price,createdAt) VALUES \
('Clásicos Verdes','verde','pollo',3,95.00,NOW()), \
('Rojos con Queso','roja','queso',2,85.00,NOW()), \
('Mole con Huevo','mole','huevo',1,99.00,NOW());"
```

### ODBC DSN (Windows)
- Instala "MySQL ODBC 8.0".
- ODBC Data Sources (64-bit) → System DSN → Add → "MySQL ODBC 8.0" → Nombre `AXEL_DSN`.

### Backend .NET 8
```powershell
cd c:\Users\manue\Documents\axel
dotnet new webapi -n chilaquiles-dotnet
cd chilaquiles-dotnet
dotnet add package MySqlConnector
dotnet add package System.Data.Odbc
# Configura appsettings.Development.json con la cadena ADO.NET
# Ejecuta
dotnet run
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
npm create vite@latest chilaquiles-ui -- --template react
cd chilaquiles-ui
npm install
npm install axios
npm run dev
```

## Ejecución local (sin Docker)
- Levanta MySQL localmente y aplica el seed de datos (comando arriba).
- Arranca `chilaquiles-java` con `mvn spring-boot:run` (puerto 8080).
- Arranca `chilaquiles-dotnet` con `dotnet run` (puerto 5000 configurable).
- Arranca `chilaquiles-ui` con `npm run dev` (puerto 5173) y configura `VITE_API_SOURCE` en `.env`.

## Endpoints esperados
- Java y .NET: `GET /api/chilaquiles?protein=pollo&salsaType=verde&spiciness=2&page=1&pageSize=20`
- `GET /api/chilaquiles/{id}`.

## Siguientes pasos
1. Generar los proyectos (Java/.NET/React) con los comandos anteriores.
2. Implementar repositorios y controladores con filtros y paginación.
3. Ajustar `.env` y probar en local.
