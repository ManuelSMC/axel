package com.axel.chilaquiles;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.context.annotation.Bean;
import org.springframework.web.cors.CorsConfiguration;
import org.springframework.web.cors.UrlBasedCorsConfigurationSource;
import org.springframework.web.filter.CorsFilter;
import org.springframework.web.bind.annotation.*;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.beans.factory.annotation.Autowired;

import java.time.Instant;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import jakarta.servlet.http.Cookie;
import jakarta.servlet.http.HttpServletResponse;
import jakarta.servlet.http.HttpServletRequest;
import io.jsonwebtoken.Jwts;
import io.jsonwebtoken.SignatureAlgorithm;
import io.jsonwebtoken.security.Keys;
import io.jsonwebtoken.io.Decoders;
import io.jsonwebtoken.security.SignatureException;

@SpringBootApplication
public class ChilaquilesApplication {
    public static void main(String[] args) {
        SpringApplication.run(ChilaquilesApplication.class, args);
    }

    @Bean
    public CorsFilter corsFilter() {
        CorsConfiguration config = new CorsConfiguration();
        // Acepta cualquier origen (JWT via header; sin cookies)
        config.setAllowedOriginPatterns(java.util.List.of("*"));
        // Permite todos los métodos y cabeceras para evitar fallos de preflight
        config.addAllowedMethod("*");
        config.addAllowedHeader("*");
        // Opcional: cachear preflight en el navegador
        config.setMaxAge(3600L);
        // Con JWT Bearer no usamos cookies; deshabilitar credenciales
        config.setAllowCredentials(false);

        UrlBasedCorsConfigurationSource source = new UrlBasedCorsConfigurationSource();
        source.registerCorsConfiguration("/**", config);
        return new CorsFilter(source);
    }
}

@RestController
@RequestMapping("/api")
class ApiController {

    @Autowired
    JdbcTemplate jdbc;

    // Health endpoint removed per request

    // JWT settings (issuer/audience/key) from env or defaults
    private String jwtIssuer() { return System.getenv().getOrDefault("JWT_ISSUER", "chilaquiles-auth"); }
    private String jwtAudience() { return System.getenv().getOrDefault("JWT_AUDIENCE", "chilaquiles-clients"); }
    private byte[] jwtKey() {
        String key = System.getenv().getOrDefault("JWT_KEY", "dev-secret-change");
        // if base64 provided, decode; else use raw string bytes
        try { return Decoders.BASE64.decode(key); } catch (Exception e) { return key.getBytes(java.nio.charset.StandardCharsets.UTF_8); }
    }

    private Integer requireAuth(HttpServletRequest request) {
        String auth = request.getHeader("Authorization");
        if (auth != null && auth.startsWith("Bearer ")) {
            String token = auth.substring(7);
            try {
                io.jsonwebtoken.JwtParser parser = Jwts.parserBuilder()
                        .setSigningKey(Keys.hmacShaKeyFor(jwtKey()))
                        .requireIssuer(jwtIssuer())
                        .requireAudience(jwtAudience())
                        .build();
                io.jsonwebtoken.Claims claims = parser.parseClaimsJws(token).getBody();
                Object uidObj = claims.get("uid");
                if (uidObj instanceof Integer) {
                    return (Integer) uidObj;
                }
                // If serialized as String/Long
                if (uidObj instanceof String) {
                    String s = (String) uidObj;
                    if (!s.isBlank()) return Integer.parseInt(s);
                }
                if (uidObj instanceof Number) {
                    Number n = (Number) uidObj;
                    return n.intValue();
                }
            } catch (io.jsonwebtoken.JwtException ex) {
                // invalid token
            }
        }
        throw new org.springframework.web.server.ResponseStatusException(org.springframework.http.HttpStatus.UNAUTHORIZED);
    }

    @PostMapping("/auth/register")
    public Map<String, Object> register(@RequestBody Map<String, String> body) {
        String fullName = body.getOrDefault("fullName", "");
        String username = body.getOrDefault("username", "");
        String password = body.getOrDefault("password", "");
        String role = body.getOrDefault("role", "user");
        if (!"admin".equalsIgnoreCase(role) && !"user".equalsIgnoreCase(role)) { role = "user"; }
        if (fullName.isBlank() || username.isBlank() || password.isBlank()) {
            throw new org.springframework.web.server.ResponseStatusException(org.springframework.http.HttpStatus.BAD_REQUEST, "Campos requeridos");
        }
        // Simple SHA-256 hash with prefix salt (demo)
        String hashSql = "SHA2(CONCAT('salt:', ?), 256)";
        // Ensure unique username
        List<Map<String, Object>> exists = jdbc.query("SELECT id FROM users WHERE username = ?", new Object[]{username}, (rs, i) -> Map.of("id", rs.getInt("id")));
        if (!exists.isEmpty()) {
            throw new org.springframework.web.server.ResponseStatusException(org.springframework.http.HttpStatus.CONFLICT, "Usuario ya existe");
        }
        jdbc.update("INSERT INTO users(full_name, username, password_hash, role, is_active) VALUES (?, ?, " + hashSql + ", ?, 1)", fullName, username, password, role.toLowerCase());
        return Map.of("ok", true);
    }

    @PostMapping("/auth/login")
    public Map<String, Object> login(@RequestBody Map<String, String> body, HttpServletResponse response) {
        String username = body.getOrDefault("username", "");
        String password = body.getOrDefault("password", "");
        List<Map<String, Object>> rows = jdbc.query(
            "SELECT id, COALESCE(role,'user') AS role FROM users WHERE username = ? AND password_hash = SHA2(CONCAT('salt:', ?), 256) AND IFNULL(is_active,1) = 1",
            new Object[]{username, password}, (rs, i) -> Map.of("id", rs.getInt("id"), "role", rs.getString("role"))
        );
        if (rows.isEmpty()) {
            throw new org.springframework.web.server.ResponseStatusException(org.springframework.http.HttpStatus.UNAUTHORIZED, "Credenciales inválidas");
        }
        int userId = (Integer) rows.get(0).get("id");
        String role = (String) rows.get(0).get("role");
        Instant now = Instant.now();
        String token = Jwts.builder()
                .setIssuer(jwtIssuer())
                .setAudience(jwtAudience())
                .setSubject("user:" + userId)
                .claim("uid", userId)
                .claim("role", role)
                .setIssuedAt(java.util.Date.from(now))
                .setExpiration(java.util.Date.from(now.plusSeconds(3600)))
                .signWith(Keys.hmacShaKeyFor(jwtKey()), SignatureAlgorithm.HS256)
                .compact();
        return Map.of("ok", true, "role", role, "token", token);
    }
    @GetMapping("/me")
    public Map<String, Object> me(HttpServletRequest request) {
        Integer uid = requireAuth(request);
        Map<String, Object> row = jdbc.query("SELECT id, username, full_name, role FROM users WHERE id = ?", new Object[]{uid}, rs -> {
            if (rs.next()) {
                return Map.of(
                        "id", rs.getInt("id"),
                        "username", rs.getString("username"),
                        "fullName", rs.getString("full_name"),
                        "role", rs.getString("role")
                );
            }
            throw new org.springframework.web.server.ResponseStatusException(org.springframework.http.HttpStatus.NOT_FOUND);
        });
        return row;
    }

    private void requireAdmin(HttpServletRequest request) {
        Integer uid = requireAuth(request);
        String role = jdbc.queryForObject("SELECT role FROM users WHERE id = ?", new Object[]{uid}, String.class);
        if (!"admin".equalsIgnoreCase(role)) {
            throw new org.springframework.web.server.ResponseStatusException(org.springframework.http.HttpStatus.FORBIDDEN);
        }
    }

    @GetMapping("/admin/users")
        public List<Map<String, Object>> listUsers(HttpServletRequest request,
                               @RequestParam(name = "includeInactive", defaultValue = "false") boolean includeInactive) {
        requireAdmin(request);
        String sql = includeInactive ?
            "SELECT id, username, full_name, role, IFNULL(is_active,1) AS is_active FROM users ORDER BY id" :
            "SELECT id, username, full_name, role, IFNULL(is_active,1) AS is_active FROM users WHERE IFNULL(is_active,1)=1 ORDER BY id";
        return jdbc.query(sql, (rs, i) -> Map.of(
                "id", rs.getInt("id"),
                "username", rs.getString("username"),
                "fullName", rs.getString("full_name"),
            "role", rs.getString("role"),
            "isActive", rs.getInt("is_active") == 1
        ));
    }

    @PostMapping("/admin/users")
    public Map<String, Object> createUser(HttpServletRequest request, @RequestBody Map<String, String> body) {
        requireAdmin(request);
        String fullName = body.getOrDefault("fullName", "");
        String username = body.getOrDefault("username", "");
        String password = body.getOrDefault("password", "");
        String role = body.getOrDefault("role", "user");
        if (fullName.isBlank() || username.isBlank() || password.isBlank()) {
            throw new org.springframework.web.server.ResponseStatusException(org.springframework.http.HttpStatus.BAD_REQUEST, "Campos requeridos");
        }
        List<Map<String, Object>> exists = jdbc.query("SELECT id FROM users WHERE username = ?", new Object[]{username}, (rs, i) -> Map.of("id", rs.getInt("id")));
        if (!exists.isEmpty()) {
            throw new org.springframework.web.server.ResponseStatusException(org.springframework.http.HttpStatus.CONFLICT, "Usuario ya existe");
        }
        String hashSql = "SHA2(CONCAT('salt:', ?), 256)";
        int affected = jdbc.update("INSERT INTO users(full_name, username, password_hash, role, is_active) VALUES (?, ?, " + hashSql + ", ?, 1)", fullName, username, password, role);
        if (affected == 1) { return Map.of("ok", true); }
        throw new org.springframework.web.server.ResponseStatusException(org.springframework.http.HttpStatus.INTERNAL_SERVER_ERROR);
    }

    @PutMapping("/admin/users/{id}")
    public Map<String, Object> updateUser(HttpServletRequest request, @PathVariable("id") int id, @RequestBody Map<String, String> body) {
        requireAdmin(request);
        String role = body.getOrDefault("role", "user");
        String fullName = body.getOrDefault("fullName", null);
        String username = body.getOrDefault("username", null);
        List<Object> args = new java.util.ArrayList<>();
        StringBuilder set = new StringBuilder();
        if (fullName != null) { set.append("full_name = ?, "); args.add(fullName); }
        if (username != null) { set.append("username = ?, "); args.add(username); }
        set.append("role = ?"); args.add(role);
        args.add(id);
        int affected = jdbc.update("UPDATE users SET " + set + " WHERE id = ?", args.toArray());
        if (affected == 1) return Map.of("ok", true);
        throw new org.springframework.web.server.ResponseStatusException(org.springframework.http.HttpStatus.NOT_FOUND);
    }

    @DeleteMapping("/admin/users/{id}")
    public Map<String, Object> deactivateUser(HttpServletRequest request, @PathVariable("id") int id) {
        requireAdmin(request);
        int affected = jdbc.update("UPDATE users SET is_active = 0 WHERE id = ?", id);
        if (affected == 1) return Map.of("ok", true);
        throw new org.springframework.web.server.ResponseStatusException(org.springframework.http.HttpStatus.NOT_FOUND);
    }

    @PostMapping("/admin/users/{id}/restore")
    public Map<String, Object> restoreUser(HttpServletRequest request, @PathVariable("id") int id) {
        requireAdmin(request);
        int affected = jdbc.update("UPDATE users SET is_active = 1 WHERE id = ?", id);
        if (affected == 1) return Map.of("ok", true);
        throw new org.springframework.web.server.ResponseStatusException(org.springframework.http.HttpStatus.NOT_FOUND);
    }

    @PostMapping("/auth/logout")
    public Map<String, Object> logout() {
        // JWT logout is client-side (discard token)
        return Map.of("ok", true);
    }

    @GetMapping("/chilaquiles")
        public List<Map<String, Object>> list(
            HttpServletRequest request,
            @RequestParam(name = "salsaType", required = false) String salsaType,
            @RequestParam(name = "protein", required = false) String protein,
            @RequestParam(name = "spiciness", required = false) Integer spiciness,
            @RequestParam(name = "includeInactive", defaultValue = "false") boolean includeInactive,
            @RequestParam(name = "page", defaultValue = "1") int page,
            @RequestParam(name = "pageSize", defaultValue = "10") int pageSize
    ) {
        requireAuth(request);
        int offset = Math.max(0, (page - 1) * pageSize);
        StringBuilder sql = new StringBuilder("SELECT id,name,salsaType,protein,spiciness,price,createdAt,is_active FROM chilaquiles");
        List<Object> args = new java.util.ArrayList<>();
        List<String> where = new java.util.ArrayList<>();
        if (!includeInactive) { where.add("is_active = 1"); }
        if (salsaType != null && !salsaType.isBlank()) { where.add("salsaType = ?"); args.add(salsaType); }
        if (protein != null && !protein.isBlank()) { where.add("protein = ?"); args.add(protein); }
        if (spiciness != null) { where.add("spiciness = ?"); args.add(spiciness); }
        if (!where.isEmpty()) { sql.append(" WHERE ").append(String.join(" AND ", where)); }
        sql.append(" ORDER BY id LIMIT ? OFFSET ?");
        args.add(pageSize); args.add(offset);
        return jdbc.query(sql.toString(), args.toArray(), (rs, i) -> Map.of(
                "id", rs.getInt("id"),
                "name", rs.getString("name"),
                "salsaType", rs.getString("salsaType"),
                "protein", rs.getString("protein"),
                "spiciness", rs.getInt("spiciness"),
                "price", rs.getBigDecimal("price"),
                "createdAt", rs.getTimestamp("createdAt").toInstant(),
                "isActive", rs.getBoolean("is_active")
        ));
    }

    @GetMapping("/chilaquiles/{id}")
    public Map<String, Object> get(HttpServletRequest request, @PathVariable("id") int id) {
        requireAuth(request);
        return jdbc.query("SELECT id,name,salsaType,protein,spiciness,price,createdAt FROM chilaquiles WHERE id = ?",
                new Object[]{id}, rs -> {
                    if (rs.next()) {
                        return Map.of(
                                "id", rs.getInt("id"),
                                "name", rs.getString("name"),
                                "salsaType", rs.getString("salsaType"),
                                "protein", rs.getString("protein"),
                                "spiciness", rs.getInt("spiciness"),
                                "price", rs.getBigDecimal("price"),
                                "createdAt", rs.getTimestamp("createdAt").toInstant()
                        );
                    }
                    throw new org.springframework.web.server.ResponseStatusException(org.springframework.http.HttpStatus.NOT_FOUND);
                });
    }

    @PostMapping("/chilaquiles")
    public Map<String, Object> create(HttpServletRequest request, @RequestBody Map<String, Object> body) {
        requireAuth(request);
        String name = (String) body.getOrDefault("name", "");
        String salsaType = (String) body.getOrDefault("salsaType", "");
        String protein = (String) body.getOrDefault("protein", "");
        Integer spiciness = (Integer) body.getOrDefault("spiciness", 0);
        java.math.BigDecimal price = new java.math.BigDecimal(String.valueOf(body.getOrDefault("price", "0")));
        if (name.isBlank() || salsaType.isBlank() || protein.isBlank() || spiciness == null) {
            throw new org.springframework.web.server.ResponseStatusException(org.springframework.http.HttpStatus.BAD_REQUEST, "Campos requeridos");
        }
        int affected = jdbc.update("INSERT INTO chilaquiles(name,salsaType,protein,spiciness,price,createdAt,is_active) VALUES (?,?,?,?,?,NOW(),1)",
                name, salsaType, protein, spiciness, price);
        if (affected == 1) {
            Integer id = jdbc.queryForObject("SELECT LAST_INSERT_ID()", Integer.class);
            return Map.of("ok", true, "id", id);
        }
        throw new org.springframework.web.server.ResponseStatusException(org.springframework.http.HttpStatus.INTERNAL_SERVER_ERROR, "No se pudo crear");
    }

    @PutMapping("/chilaquiles/{id}")
    public Map<String, Object> update(HttpServletRequest request, @PathVariable("id") int id, @RequestBody Map<String, Object> body) {
        requireAuth(request);
        String name = (String) body.getOrDefault("name", "");
        String salsaType = (String) body.getOrDefault("salsaType", "");
        String protein = (String) body.getOrDefault("protein", "");
        Integer spiciness = (Integer) body.getOrDefault("spiciness", 0);
        java.math.BigDecimal price = new java.math.BigDecimal(String.valueOf(body.getOrDefault("price", "0")));
        int affected = jdbc.update("UPDATE chilaquiles SET name=?, salsaType=?, protein=?, spiciness=?, price=? WHERE id=?",
                name, salsaType, protein, spiciness, price, id);
        if (affected == 1) { return Map.of("ok", true); }
        throw new org.springframework.web.server.ResponseStatusException(org.springframework.http.HttpStatus.NOT_FOUND, "No existe");
    }

    @DeleteMapping("/chilaquiles/{id}")
    public Map<String, Object> softDelete(HttpServletRequest request, @PathVariable("id") int id) {
        requireAuth(request);
        int affected = jdbc.update("UPDATE chilaquiles SET is_active=0 WHERE id=?", id);
        if (affected == 1) { return Map.of("ok", true); }
        throw new org.springframework.web.server.ResponseStatusException(org.springframework.http.HttpStatus.NOT_FOUND, "No existe");
    }

    @PostMapping("/chilaquiles/{id}/restore")
    public Map<String, Object> restore(HttpServletRequest request, @PathVariable("id") int id) {
        requireAuth(request);
        int affected = jdbc.update("UPDATE chilaquiles SET is_active=1 WHERE id=?", id);
        if (affected == 1) { return Map.of("ok", true); }
        throw new org.springframework.web.server.ResponseStatusException(org.springframework.http.HttpStatus.NOT_FOUND, "No existe");
    }
}
