#include "screenmap.h"
#include <algorithm>
#include <array>
#include <cmath>
#include <cstdio>
#include <exception>
#include <limits>
#include <memory>
#include <numeric>
#include <set>
#include <stdexcept>
#include <string>
#include <tuple>
#include <vector>

struct SmScene {
    SmConfig config{};
    std::vector<SmLine> lines;
    std::vector<SmCircle> circles;
    std::vector<SmLabel> labels;
    std::vector<SmRegion> regions;
};
namespace {
thread_local std::string error;
struct Point { double x, y; };
struct Box { double l,t,r,b; };
Box far_box(const SmConfig& c) {
    // Equal normalized extents retain the selected pixel aspect ratio at every D.
    // The circle is measured against the short side and fits inside this rectangle.
    const double rx=c.radius*1.05,ry=rx;
    return {c.center_x-rx,c.center_y-ry,c.center_x+rx,c.center_y+ry};
}
double knot(double v,double left,double center,double right) {
    const double nodes[]{0,left,center,right,1};
    int i=std::min(3,static_cast<int>(v*4));
    return nodes[i]+(nodes[i+1]-nodes[i])*(v*4-i);
}
Point map(Point p,const SmConfig& c) {
    auto f=far_box(c);
    return {knot(p.x,f.l/2,c.center_x,(1+f.r)/2),knot(p.y,f.t/2,c.center_y,(1+f.b)/2)};
}
void line(SmScene& s, Point a, Point b, int layer, int kind=0) {
    s.lines.push_back({a.x,a.y,b.x,b.y,layer,kind});
}
void label(SmScene& s, Point p, int layer, int id, const std::string& text, double cap=0) {
    SmLabel l{p.x,p.y,layer,id,cap,{}};
    std::snprintf(l.text,sizeof(l.text),"%s",text.c_str());
    s.labels.push_back(l);
}
double cross(Point a, Point b, Point c) { return (b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x); }
bool inside(const SmRegion& r, Point p) {
    const Point a{r.ax,r.ay}, b{r.bx,r.by}, c{r.cx,r.cy};
    const auto u=cross(a,b,p), v=cross(b,c,p), w=cross(c,a,p);
    constexpr double eps=1e-12;
    return (u>=-eps&&v>=-eps&&w>=-eps)||(u<=eps&&v<=eps&&w<=eps);
}
double angle(const SmRegion& r) {
    constexpr double pi=3.14159265358979323846;
    double a=std::atan2((r.ay+r.by+r.cy)/3-.5,(r.ax+r.bx+r.cx)/3-.5)+.75*pi;
    if(a<0) a+=2*pi;
    return a;
}
void zones(SmScene& s) {
    std::array<std::vector<SmRegion>,2> rings;
    for(int row=0;row<4;++row) for(int col=0;col<4;++col) {
        Point a{col*.25,row*.25}, b{(col+1)*.25,row*.25};
        Point c{(col+1)*.25,(row+1)*.25}, d{col*.25,(row+1)*.25};
        int ring=(row>=1&&row<=2&&col>=1&&col<=2)?1:2;
        auto add=[&](Point p,Point q,Point r){ rings[ring-1].push_back({p.x,p.y,q.x,q.y,r.x,r.y,ring,0}); };
        if((row+col)%2==0) {add(a,b,c);add(a,c,d);} else {add(a,b,d);add(b,c,d);}
    }
    std::set<std::array<long long,4>> edges;
    for(auto& ring:rings) {
        std::sort(ring.begin(),ring.end(),[](const auto& a,const auto& b){return angle(a)<angle(b);});
        int index=0;
        for(auto r:ring) {
            r.index=++index;
            Point a=map({r.ax,r.ay},s.config), b=map({r.bx,r.by},s.config), c=map({r.cx,r.cy},s.config);
            r.ax=a.x;r.ay=a.y;r.bx=b.x;r.by=b.y;r.cx=c.x;r.cy=c.y;
            s.regions.push_back(r);
            for(auto edge:std::array<std::pair<Point,Point>,3>{{{a,b},{b,c},{c,a}}}) {
                auto [p,q]=edge;
                if(std::tie(p.x,p.y)>std::tie(q.x,q.y)) std::swap(p,q);
                std::array<long long,4> key{std::llround(p.x*1e10),std::llround(p.y*1e10),std::llround(q.x*1e10),std::llround(q.y*1e10)};
                if(edges.insert(key).second) line(s,p,q,SM_PRIMITIVE);
            }
            Point centroid{(a.x+b.x+c.x)/3,(a.y+b.y+c.y)/3};
            // Keep labels away from the central disk, preserving their original region.
            const double aspect=static_cast<double>(s.config.aspect_p)/s.config.aspect_q;
            const double sx=std::min(1.0,1/aspect),sy=std::min(1.0,aspect);
            auto radial=[&](Point p){return std::hypot((p.x-s.config.center_x)/sx,(p.y-s.config.center_y)/sy);};
            if(radial(centroid)<s.config.radius*1.65) {
                Point far=a;
                for(Point p:{b,c}) if(radial(p)>radial(far)) far=p;
                centroid={centroid.x*.4+far.x*.6,centroid.y*.4+far.y*.6};
            }
            label(s,centroid,SM_ZONES,r.ring*100+r.index,std::to_string(r.ring)+"."+std::to_string(r.index));
        }
    }
    const double aspect=static_cast<double>(s.config.aspect_p)/s.config.aspect_q;
    s.circles.push_back({s.config.center_x,s.config.center_y,s.config.radius*std::min(1.0,1/aspect),s.config.radius*std::min(1.0,aspect),SM_PRIMITIVE,0});
    label(s,{s.config.center_x,s.config.center_y},SM_ZONES,1,"1",s.config.radius*2*720*.70);
}
void levels(SmScene& s) {
    const auto f=far_box(s.config);
    const double ys[]{0,f.t/2,s.config.center_y,(1+f.b)/2,1};
    const char* texts[]{"TOP","MIDTOP","MIDBOTTOM","BOTTOM"};
    for(double y:ys) line(s,{0,y},{1,y},SM_LEVELS);
    line(s,{0,0},{0,1},SM_LEVELS);line(s,{1,0},{1,1},SM_LEVELS);
    for(int i=0;i<4;++i) label(s,{.5,ys[i]+(ys[i+1]-ys[i])*.24},SM_LEVELS,i,texts[i]);
}
Box mix(Box a,Box b,double u) {return {a.l+(b.l-a.l)*u,a.t+(b.t-a.t)*u,a.r+(b.r-a.r)*u,a.b+(b.b-a.b)*u};}
Point corner(Box b,int i) {switch(i){case 0:return {b.l,b.t};case 1:return {b.r,b.t};case 2:return {b.r,b.b};default:return {b.l,b.b};}}
void frame(SmScene& s,Box b,int layer,int kind=0) {
    for(int i=0;i<4;++i) {
        int edge_kind=kind;
        if(layer==SM_PGFA)edge_kind|=i%2==0 ? SM_PGFA_CROSSBAR : SM_PGFA_SIDE_WALL;
        line(s,corner(b,i),corner(b,(i+1)%4),layer,edge_kind);
    }
}
void open_pgfa_rays(SmScene& s,Box near,Box far,int columns) {
    const double near_step=(near.r-near.l)/columns;
    const double far_step=(far.r-far.l)/columns;
    for(bool left:{true,false}) {
        const double edge=left?0:1;
        const double direction=left?-1:1;
        const double near_side=left?near.l:near.r;
        const double far_side=left?far.l:far.r;
        const int visible_columns=static_cast<int>(std::abs(edge-far_side)/far_step);
        // Keep the original projected column pitch and sample fewer columns far
        // to the sides, where a small D7 would otherwise produce hundreds of rays.
        const int stride=std::max(1,(visible_columns+columns-1)/columns);
        for(int column=1;column<=visible_columns;column+=stride) {
            const double near_x=near_side+direction*column*near_step;
            const double far_x=far_side+direction*column*far_step;
            if(far_x<=0||far_x>=1)continue;
            const bool clipped=near_x<0||near_x>1;
            const double entry=clipped?(edge-near_x)/(far_x-near_x):0;
            for(bool top:{true,false}) {
                const double near_y=top?near.t:near.b;
                const double far_y=top?far.t:far.b;
                line(s,{clipped?edge:near_x,std::lerp(near_y,far_y,entry)},
                    {far_x,far_y},SM_PGFA,SM_PGFA_OPEN_RAY);
            }
        }
    }
}
void perspective(SmScene& s) {
    const auto& c=s.config;
    const Box near{.055,.055,.945,.945},far=far_box(c);
    std::array<Box,8> frames{};
    double total=0;
    for(int j=1;j<8;++j)total+=std::max(8.0,c.depth_font-(c.depth_shift?j*2:0))+6;
    double cumulative=0;
    const bool above=c.center_y>=.5;
    Box previous{0,0,1,1};
    for(int j=0;j<8;++j) {
        if(j>0)cumulative+=std::max(8.0,c.depth_font-(c.depth_shift?j*2:0))+6;
        frames[j]=mix(near,far,j==0?0:cumulative/total);
        frame(s,frames[j],SM_DEPTH,j);
        const auto b=frames[j];
        const double gap=above?b.t-previous.t:previous.b-b.b;
        const double y=above?(b.t+previous.t)/2:(b.b+previous.b)/2;
        const double reference_height=720.0*c.aspect_q/std::min(c.aspect_p,c.aspect_q);
        label(s,{b.l+(b.r-b.l)*.10,y},SM_DEPTH,j,"D"+std::to_string(j),std::max(5.0,gap*reference_height*.78));
        previous=b;
    }
    // Depth rays are drawn as the topmost grid layer in the renderer.
    for(int i=0;i<4;++i)line(s,corner(near,i),corner(far,i),SM_DEPTH,SM_DEPTH_RAY);
    const int sub=static_cast<int>(c.pgfa_subdivisions);
    for(int j=0;j<7;++j)for(int k=0;k<sub;++k)frame(s,mix(frames[j],frames[j+1],static_cast<double>(k)/sub),SM_PGFA,k==0?1:0);
    frame(s,far,SM_PGFA,1);
    // A balanced, deliberately sparse field; density adds subdivisions.
    const double a=static_cast<double>(c.aspect_p)/c.aspect_q;
    const int nx=std::max(2,static_cast<int>(std::round(3*std::sqrt(a))))*sub;
    const int ny=std::max(2,static_cast<int>(std::round(3/std::sqrt(a))))*sub;
    for(int i=0;i<=nx;++i) {
        const double u=static_cast<double>(i)/nx;
        for(int side:{0,1})line(s,{near.l+(near.r-near.l)*u,side?near.b:near.t},{far.l+(far.r-far.l)*u,side?far.b:far.t},SM_PGFA);
    }
    for(int i=1;i<ny;++i) {
        const double u=static_cast<double>(i)/ny;
        for(int side:{0,1})line(s,{side?near.r:near.l,near.t+(near.b-near.t)*u},{side?far.r:far.l,far.t+(far.b-far.t)*u},SM_PGFA,SM_PGFA_SIDE_WALL);
    }
    open_pgfa_rays(s,near,far,nx);
    label(s,{far.r,far.b},SM_PGFA,0,"PGFA");
}
void primitive(SmScene& s) {
    const auto& c=s.config;
    const uint32_t nx=c.density,ny=c.primitive_rows;
    for(uint32_t x=0;x<=nx;++x)line(s,{static_cast<double>(x)/nx,0},{static_cast<double>(x)/nx,1},SM_PRIMITIVE);
    for(uint32_t y=0;y<=ny;++y)line(s,{0,static_cast<double>(y)/ny},{1,static_cast<double>(y)/ny},SM_PRIMITIVE);
    for(uint32_t y=0;y<ny;++y)for(uint32_t x=0;x<nx;++x)
        label(s,{(x+.5)/nx,(y+.5)/ny},SM_ZONES,static_cast<int>(y*nx+x+1),std::to_string(y*nx+x+1));
    label(s,{.94,.02},SM_PRIMITIVE,0,std::to_string(nx)+"x"+std::to_string(ny));
    const double a=static_cast<double>(c.aspect_p)/c.aspect_q;
    s.circles.push_back({.5,.5,c.primitive_radius*std::min(1.0,1/a),c.primitive_radius*std::min(1.0,a),SM_PRIMITIVE,1});
}
SmConfig validate(const SmConfig* c) {
    if(!c||c->struct_size!=sizeof(SmConfig)||c->abi!=SM_ABI) throw std::invalid_argument("Incompatible configuration/ABI");
    if(c->mode>1||c->reserved!=0||!std::isfinite(c->center_x)||!std::isfinite(c->center_y)||!std::isfinite(c->radius)||!std::isfinite(c->depth_font)||!std::isfinite(c->primitive_radius)||c->depth_shift>1) throw std::invalid_argument("Invalid mode or non-finite parameter");
    auto v=*c;
    if(!v.aspect_p||!v.aspect_q||v.aspect_p>32||v.aspect_q>32) throw std::invalid_argument("Invalid aspect ratio");
    auto gcd=std::gcd(v.aspect_p,v.aspect_q);v.aspect_p/=gcd;v.aspect_q/=gcd;
    const auto p=std::max(v.aspect_p,v.aspect_q),q=std::min(v.aspect_p,v.aspect_q);
    if(!((p==1&&q==1)||(p==4&&q==3)||(p==16&&q==9))) throw std::invalid_argument("Unsupported aspect ratio");
    const auto max_cols=static_cast<uint32_t>(std::round(16.0*v.aspect_p/p));
    const auto max_rows=static_cast<uint32_t>(std::round(16.0*v.aspect_q/p));
    if(v.density<1||v.density>max_cols||v.primitive_rows<1||v.primitive_rows>max_rows||v.pgfa_subdivisions<1||v.pgfa_subdivisions>6) throw std::invalid_argument("Grid density outside supported range");
    v.center_x=std::clamp(v.center_x,.08,.92);v.center_y=std::clamp(v.center_y,.08,.92);
    double limit=std::min({v.center_x-.06,.94-v.center_x,v.center_y-.06,.94-v.center_y})/1.05;
    v.radius=std::clamp(v.radius,.006,std::min(.28,limit));
    v.depth_font=std::clamp(v.depth_font,8.0,72.0);
    v.primitive_radius=std::clamp(v.primitive_radius,.005,.2);
    return v;
}
template<class T> const T* items(const std::vector<T>& v,uint32_t* n) { if(n)*n=static_cast<uint32_t>(v.size()); return v.empty()?nullptr:v.data(); }
}
uint32_t sm_abi_version(void) {return SM_ABI;}
uint32_t sm_struct_size(uint32_t kind) {
    switch(kind) {case 0:return sizeof(SmConfig);case 1:return sizeof(SmLine);case 2:return sizeof(SmCircle);case 3:return sizeof(SmLabel);case 4:return sizeof(SmRegion);default:return 0;}
}
int32_t sm_build(const SmConfig* config,SmScene** result) {
    if(!result) {error="Null result pointer";return 1;}
    *result=nullptr;
    try {
        auto scene=std::make_unique<SmScene>();scene->config=validate(config);
        if(scene->config.mode==0)zones(*scene);else primitive(*scene);
        levels(*scene);perspective(*scene);
        *result=scene.release();error.clear();return 0;
    } catch(const std::exception& e){error=e.what();return 1;} catch(...){error="Unexpected native failure";return 2;}
}
void sm_destroy(SmScene* s) {delete s;}
const char* sm_last_error(void){return error.c_str();}
const SmConfig* sm_config(const SmScene* s){return s?&s->config:nullptr;}
const SmLine* sm_lines(const SmScene* s,uint32_t* n){if(!s){if(n)*n=0;return nullptr;}return items(s->lines,n);}
const SmCircle* sm_circles(const SmScene* s,uint32_t* n){if(!s){if(n)*n=0;return nullptr;}return items(s->circles,n);}
const SmLabel* sm_labels(const SmScene* s,uint32_t* n){if(!s){if(n)*n=0;return nullptr;}return items(s->labels,n);}
const SmRegion* sm_regions(const SmScene* s,uint32_t* n){if(!s){if(n)*n=0;return nullptr;}return items(s->regions,n);}
int32_t sm_hit_test(const SmScene* s,double u,double v) {
    if(!s||!std::isfinite(u)||!std::isfinite(v)||u<0||v<0||u>1||v>1)return 0;
    if(s->config.mode==1)return 1000+std::min(static_cast<int>(s->config.primitive_rows)-1,static_cast<int>(v*s->config.primitive_rows))*static_cast<int>(s->config.density)+std::min(static_cast<int>(s->config.density)-1,static_cast<int>(u*s->config.density))+1;
    const auto& c=s->circles.front();
    if(std::pow((u-c.x)/c.rx,2)+std::pow((v-c.y)/c.ry,2)<=1)return 1;
    for(const auto& r:s->regions)if(inside(r,{u,v}))return r.ring*100+r.index;
    return 0;
}
