#pragma once
#include <stdint.h>
#ifdef _WIN32
#ifdef SCREENMAP_EXPORTS
#define SM_API __declspec(dllexport)
#else
#define SM_API __declspec(dllimport)
#endif
#else
#define SM_API
#endif
#ifdef __cplusplus
extern "C" {
#endif
enum { SM_ABI = 2, SM_PGFA = 0, SM_DEPTH = 1, SM_LEVELS = 2, SM_ZONES = 3, SM_PRIMITIVE = 4 };
/* SmLine.kind values are layer-specific. */
enum { SM_PGFA_SIDE_WALL = 2, SM_PGFA_CROSSBAR = 4, SM_DEPTH_RAY = 8, SM_PGFA_OPEN_RAY = 16 };
typedef struct SmConfig {
    uint32_t struct_size, abi, mode, aspect_p, aspect_q, density, pgfa_subdivisions, primitive_rows;
    double center_x, center_y, radius, depth_font, primitive_radius;
    uint32_t depth_shift, reserved;
} SmConfig;
typedef struct SmLine {
    double x1, y1, x2, y2;
    int32_t layer, kind;
} SmLine;
typedef struct SmCircle {
    double x, y, rx, ry;
    int32_t layer, filled;
} SmCircle;
typedef struct SmLabel {
    double x, y;
    int32_t layer, id;
    double font_cap;
    char text[24];
} SmLabel;
typedef struct SmRegion {
    double ax, ay, bx, by, cx, cy;
    int32_t ring, index;
} SmRegion;
typedef struct SmScene SmScene;
SM_API uint32_t sm_abi_version(void);
SM_API uint32_t sm_struct_size(uint32_t kind);
SM_API int32_t sm_build(const SmConfig* config, SmScene** result);
SM_API void sm_destroy(SmScene* scene);
SM_API const char* sm_last_error(void);
SM_API const SmConfig* sm_config(const SmScene* scene);
SM_API const SmLine* sm_lines(const SmScene* scene, uint32_t* count);
SM_API const SmCircle* sm_circles(const SmScene* scene, uint32_t* count);
SM_API const SmLabel* sm_labels(const SmScene* scene, uint32_t* count);
SM_API const SmRegion* sm_regions(const SmScene* scene, uint32_t* count);
/* 1 = center; 101..108 = inner; 201..224 = outer; 1001.. = primitive cells; 0 = outside. */
SM_API int32_t sm_hit_test(const SmScene* scene, double u, double v);
#ifdef __cplusplus
}
#endif
