#include "screenmap.h"
#include <algorithm>
#include <cmath>
#include <iostream>
#include <limits>
#include <memory>
#include <stdexcept>
#include <vector>
namespace {
int assertions=0;
void check(bool ok,const char* what){++assertions;if(!ok)throw std::runtime_error(what);}
bool close(double a,double b){return std::abs(a-b)<1e-9;}
SmConfig config(){SmConfig c{};c.struct_size=sizeof(c);c.abi=SM_ABI;c.aspect_p=16;c.aspect_q=9;c.density=8;c.primitive_rows=5;c.pgfa_subdivisions=1;c.center_x=.5;c.center_y=.5;c.radius=.075;c.depth_font=24;c.depth_shift=1;c.primitive_radius=.025;return c;}
using Scene=std::unique_ptr<SmScene,decltype(&sm_destroy)>;
Scene build(SmConfig c){SmScene* s=nullptr;check(sm_build(&c,&s)==0,sm_last_error());return {s,sm_destroy};}
double area(SmRegion r){return std::abs((r.bx-r.ax)*(r.cy-r.ay)-(r.by-r.ay)*(r.cx-r.ax))/2;}
void cases(){
 for(auto [p,q]:std::vector<std::pair<uint32_t,uint32_t>>{{1,1},{4,3},{3,4},{16,9},{9,16}})
 for(double x:{.08,.3,.5,.7,.92})for(double y:{.08,.3,.5,.7,.92})for(double radius:{.006,.075,.18,.27}){
  auto c=config();c.aspect_p=p;c.aspect_q=q;c.density=1;c.primitive_rows=1;c.center_x=x;c.center_y=y;c.radius=radius;auto s=build(c);
  uint32_t n=0;auto r=sm_regions(s.get(),&n);check(n==32,"32 regions");double sum=0;int inner=0,outer=0;
  for(uint32_t i=0;i<n;i++){check(area(r[i])>0,"positive area");sum+=area(r[i]);if(r[i].ring==1)check(r[i].index==++inner,"inner IDs");else check(r[i].index==++outer,"outer IDs");}
  check(close(sum,1),"frame partition");check(inner==8&&outer==24,"region counts");
  check(sm_hit_test(s.get(),x,y)==1,"center hit");
  for(int iy=0;iy<13;iy++)for(int ix=0;ix<13;ix++)check(sm_hit_test(s.get(),(ix+.27)/13,(iy+.31)/13)>0,"coverage");
  auto circles=sm_circles(s.get(),&n);check(n==1,"one center circle");auto circle=circles[0];
  check(close(circle.rx*p,circle.ry*q),"pixel circle");
  auto ls=sm_lines(s.get(),&n);int depth=0,pgfa=0;
  const double far_extent=sm_config(s.get())->radius*1.05;
  const bool fits=x-far_extent>=0&&x+far_extent<=1;
  for(uint32_t i=0;i<n;i++){auto l=ls[i];check(std::isfinite(l.x1)&&std::isfinite(l.y1)&&std::isfinite(l.x2)&&std::isfinite(l.y2),"finite");
    if(fits||l.layer==SM_PRIMITIVE||l.layer==SM_LEVELS||(l.layer==SM_PGFA&&l.kind==SM_PGFA_OPEN_RAY))
      check(l.x1>=0&&l.x1<=1&&l.y1>=0&&l.y1<=1&&l.x2>=0&&l.x2<=1&&l.y2>=0&&l.y2<=1,"visible geometry stays inside");
    if(l.layer==SM_DEPTH){depth++;
      if(l.kind==0)check(l.x1>0&&l.y1>0&&l.x1<1&&l.y1<1,"D0 inset");}
    if(l.layer==SM_PGFA)pgfa++;
  }
  check(depth==36&&pgfa>0,"all perspective layers");
  for(int kind=0;kind<8;kind++){
    double left=std::numeric_limits<double>::infinity(),top=left,right=-left,bottom=-left;
    for(uint32_t i=0;i<n;i++)if(ls[i].layer==SM_DEPTH&&ls[i].kind==kind){left=std::min({left,ls[i].x1,ls[i].x2});right=std::max({right,ls[i].x1,ls[i].x2});top=std::min({top,ls[i].y1,ls[i].y2});bottom=std::max({bottom,ls[i].y1,ls[i].y2});}
    check(close(right-left,bottom-top),"every depth rectangle retains selected AR");
    if(kind==7){check(left<=circle.x-circle.rx&&right>=circle.x+circle.rx,"D7 encloses circle x");check(top<=circle.y-circle.ry&&bottom>=circle.y+circle.ry,"D7 encloses circle y");}
  }
  auto labels=sm_labels(s.get(),&n);int dlabels=0;for(uint32_t i=0;i<n;i++)if(labels[i].layer==SM_DEPTH){dlabels++;check(labels[i].font_cap>0,"depth band font cap");}
  check(dlabels==8,"D0-D7 captions");
 }
}
void open_perspective(){
 auto c=config();
 c.center_x=.56;c.center_y=.60;c.radius=.024;c.pgfa_subdivisions=2;
 const int columns=8;
 for(double x:{.56,-.35,1.35}){
  c.center_x=x;auto s=build(c);auto projected=sm_config(s.get());
  check(close(projected->center_x,x),"horizontal perspective position retained outside frame");
  check(close(projected->center_y,.60)&&close(projected->radius,.024),"moving horizontally preserves horizon and radius");
  uint32_t n=0;auto lines=sm_lines(s.get(),&n);
  for(bool top:{true,false})for(double depth:{.25,.5,.75,.9}){
   const double radius=projected->radius*1.05;
   const double y=std::lerp(top?.055:.945,c.center_y+(top?-radius:radius),depth);
   const double pitch=std::lerp(.89,2*radius,depth)/columns;
   std::vector<double> xs;
   for(uint32_t i=0;i<n;i++){
    const auto& l=lines[i];
    if(l.layer!=SM_PGFA||(l.kind!=0&&l.kind!=SM_PGFA_OPEN_RAY)||l.y1==l.y2)continue;
    if(y<std::min(l.y1,l.y2)||y>std::max(l.y1,l.y2))continue;
    const double at=std::lerp(l.x1,l.x2,(y-l.y1)/(l.y2-l.y1));
    if(at>0&&at<1)xs.push_back(at);
   }
   std::sort(xs.begin(),xs.end());
   check(xs.size()>5&&xs.front()<pitch+1e-9&&1-xs.back()<pitch+1e-9,"open PGFA covers full width at every sampled depth");
   for(size_t i=1;i<xs.size();i++)check(close(xs[i]-xs[i-1],pitch),"uniform PGFA pitch across closed-to-open seam");
  }
  auto regions=sm_regions(s.get(),&n);double sum=0;
  for(uint32_t i=0;i<n;i++)sum+=area(regions[i]);
  check(n==32&&close(sum,1),"off-frame perspective keeps the zone partition");
  if(x<0||x>1)for(double u:{.01,.25,.75,.99})for(double v:{.01,.25,.75,.99})
   check(sm_hit_test(s.get(),u,v)>0,"zone hit testing works with perspective outside the image");
 }
}
void coupled(){
 auto c=config();auto a=build(c);c.radius=.18;auto b=build(c);
 uint32_t n=0,m=0;auto la=sm_lines(a.get(),&n);auto lb=sm_lines(b.get(),&m);
 bool depth=false,zones=false,pgfa=false,levels=false;
 for(uint32_t i=0;i<std::min(n,m);i++)if(!close(la[i].x1,lb[i].x1)||!close(la[i].y1,lb[i].y1)){
  if(la[i].layer==SM_DEPTH)depth=true;if(la[i].layer==SM_PRIMITIVE)zones=true;if(la[i].layer==SM_PGFA)pgfa=true;if(la[i].layer==SM_LEVELS)levels=true;
 }
 check(depth&&zones&&pgfa&&levels,"radius drives all geometry");
 c=config();auto sparse=build(c);c.pgfa_subdivisions=4;auto dense=build(c);
 auto p=sm_lines(sparse.get(),&n);auto q=sm_lines(dense.get(),&m);check(m>n,"PGFA density");
 std::vector<SmLine> da,db;for(uint32_t i=0;i<n;i++)if(p[i].layer==SM_DEPTH)da.push_back(p[i]);for(uint32_t i=0;i<m;i++)if(q[i].layer==SM_DEPTH)db.push_back(q[i]);
 check(da.size()==db.size(),"D count stable");for(size_t i=0;i<da.size();i++)check(close(da[i].x1,db[i].x1)&&close(da[i].y1,db[i].y1),"D positions stable");
 c=config();c.mode=1;
 for(uint32_t cols:{1u,8u,16u})for(uint32_t rows:{1u,4u,9u}){
  c.density=cols;c.primitive_rows=rows;auto s=build(c);auto labels=sm_labels(s.get(),&n);int cells=0;for(uint32_t i=0;i<n;i++)if(labels[i].layer==SM_ZONES)check(labels[i].id==++cells,"row-major cell IDs");
  check(cells==static_cast<int>(cols*rows),"primitive caption count");check(sm_hit_test(s.get(),.99,.99)==1000+static_cast<int>(cols*rows),"primitive hit");
  auto lines=sm_lines(s.get(),&n);bool d=false,l=false,g=false;for(uint32_t i=0;i<n;i++){d|=lines[i].layer==SM_DEPTH;l|=lines[i].layer==SM_LEVELS;g|=lines[i].layer==SM_PGFA;}check(d&&l&&g,"primitive keeps all layers");
 }
 for(auto [aspect_p,aspect_q]:std::vector<std::pair<uint32_t,uint32_t>>{{4,3},{3,4},{1,1},{16,9},{9,16}}){
  c=config();c.mode=1;c.aspect_p=aspect_p;c.aspect_q=aspect_q;c.density=static_cast<uint32_t>(std::round(16.0*aspect_p/std::max(aspect_p,aspect_q)));c.primitive_rows=static_cast<uint32_t>(std::round(16.0*aspect_q/std::max(aspect_p,aspect_q)));auto s=build(c);
  auto labels=sm_labels(s.get(),&n);int count=0;for(uint32_t i=0;i<n;i++)count+=labels[i].layer==SM_ZONES;
  check(count==static_cast<int>(c.density*c.primitive_rows),"16 divisions on long side for every AR");
 }
 SmScene* out=nullptr;c=config();c.abi=1;check(sm_build(&c,&out)!=0&&!out,"reject old ABI");
 c=config();c.center_x=std::numeric_limits<double>::quiet_NaN();check(sm_build(&c,&out)!=0,"reject NaN");
 c=config();c.mode=1;c.density=17;check(sm_build(&c,&out)!=0,"density capped");
 for(int i=0;i<1000;i++){auto s=build(config());check(sm_hit_test(s.get(),.5,.5)==1,"lifetime");}
}
}
int main(){try{check(SM_ABI==2&&sm_abi_version()==2,"ABI");cases();open_perspective();coupled();std::cout<<"PASS: "<<assertions<<" assertions\n";return 0;}catch(const std::exception&e){std::cerr<<e.what()<<'\n';return 1;}}
