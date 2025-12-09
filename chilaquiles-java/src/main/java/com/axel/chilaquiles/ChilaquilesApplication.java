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

@SpringBootApplication
public class ChilaquilesApplication {
    public static void main(String[] args) {
        SpringApplication.run(ChilaquilesApplication.class, args);
    }

    @Bean
    public CorsFilter corsFilter() {
        CorsConfiguration config = new CorsConfiguration();
        config.addAllowedOriginPattern("*");
        config.addAllowedHeader("*");
        config.addAllowedMethod("*");
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

    @GetMapping("/health")
    public Map<String, String> health() { return Map.of("status", "ok", "driver", "jdbc"); }

    @GetMapping("/chilaquiles")
        public List<Map<String, Object>> list(
            @RequestParam(name = "salsaType", required = false) String salsaType,
            @RequestParam(name = "protein", required = false) String protein,
            @RequestParam(name = "spiciness", required = false) Integer spiciness,
            @RequestParam(name = "page", defaultValue = "1") int page,
            @RequestParam(name = "pageSize", defaultValue = "10") int pageSize
    ) {
        int offset = Math.max(0, (page - 1) * pageSize);
        StringBuilder sql = new StringBuilder("SELECT id,name,salsaType,protein,spiciness,price,createdAt FROM chilaquiles");
        List<Object> args = new java.util.ArrayList<>();
        List<String> where = new java.util.ArrayList<>();
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
                "createdAt", rs.getTimestamp("createdAt").toInstant()
        ));
    }

    @GetMapping("/chilaquiles/{id}")
    public Map<String, Object> get(@PathVariable int id) {
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
}
