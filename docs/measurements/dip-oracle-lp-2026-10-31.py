"""Independent exact oracle for the Hartigan dip (pass 56, 2026-10-31). NOT part of the build and shares no code with EngineCommon.TailTool.

The dip of a sample is the smallest D for which some unimodal distribution function lies within D (sup norm) of the empirical one. For each position of the mode
(between two data values, or beyond them) this solves the linear program "minimise D over the values of the distribution function at the data points and its limits
either side of the mode, convex on the left of the mode and concave on the right (chord slopes by cross-multiplication), within D of the step function's value on both
sides of every jump", in exact rational arithmetic (two-phase simplex, Bland's rule), and keeps the smallest optimum. The mode is placed at a fraction 1/1000, 1/2
and 999/1000 of the gap (an atom at the mode is allowed, as in Hartigan and Hartigan). Distinct values only. Usage: python dip-oracle-lp-2026-10-31.py 1 4 16 26  ->  3/22
"""
from fractions import Fraction as Fr
from fractions import Fraction as Fr

def simplex_min(c, A, b):
    """min c.x s.t. A x <= b, x>=0. Exact, two-phase, Bland. Returns (value, x) or None if infeasible."""
    m=len(A); n=len(c)
    # rows: A x + s = b ; if b<0 multiply by -1 and use artificial
    rows=[]; basis=[]
    nart=sum(1 for i in range(m) if b[i]<0)
    total=n+m+nart
    T=[]; ai=0
    for i in range(m):
        r=[Fr(0)]*(total+1)
        if b[i]>=0:
            for j in range(n): r[j]=Fr(A[i][j])
            r[n+i]=Fr(1); r[-1]=Fr(b[i]); basis.append(n+i)
        else:
            for j in range(n): r[j]=-Fr(A[i][j])
            r[n+i]=Fr(-1); r[n+m+ai]=Fr(1); r[-1]=-Fr(b[i]); basis.append(n+m+ai); ai+=1
        T.append(r)
    def run(obj, allowed):
        # obj: list len total ; minimize
        while True:
            # reduced costs
            z=[Fr(0)]*(total+1)
            for i in range(m):
                cb=obj[basis[i]]
                if cb!=0:
                    for j in range(total+1): z[j]+=cb*T[i][j]
            enter=None
            for j in range(total):
                if j in allowed and obj[j]-z[j]<0: enter=j;break
            if enter is None: return z[-1]
            best=None;leave=None
            for i in range(m):
                if T[i][enter]>0:
                    ratio=T[i][-1]/T[i][enter]
                    if best is None or ratio<best or (ratio==best and basis[i]<basis[leave]): best=ratio;leave=i
            if leave is None: raise Exception("unbounded")
            p=T[leave][enter]
            T[leave]=[v/p for v in T[leave]]
            for i in range(m):
                if i!=leave and T[i][enter]!=0:
                    f=T[i][enter]; T[i]=[a-f*bb for a,bb in zip(T[i],T[leave])]
            basis[leave]=enter
    if nart:
        obj=[Fr(0)]*total
        for k in range(nart): obj[n+m+k]=Fr(1)
        v=run(obj,set(range(total)))
        if v>0: return None
        # drive artificials out (degenerate) - simple: forbid them afterwards
    obj=[Fr(0)]*total
    for j in range(n): obj[j]=Fr(c[j])
    allowed=set(range(n+m))
    val=run(obj,allowed)
    x=[Fr(0)]*n
    for i in range(m):
        if basis[i]<n: x[basis[i]]=T[i][-1]
    return val,x



def dip_lp2(xs, thetas=(Fr(1,1000),Fr(1,2),Fr(999,1000))):
    xs=sorted(xs); n=len(xs)
    u=[];w=[]
    for v in xs:
        if u and u[-1]==v: w[-1]+=1
        else: u.append(v); w.append(1)
    m=len(u)
    cum=[Fr(0)]
    for k in w: cum.append(cum[-1]+Fr(k,n))
    best=None
    # p = number of left points (0..m): left = points 0..p-1, right = p..m-1 ; gap between p-1 and p
    for p in range(0,m+1):
        for th in (thetas if 0<p<m else (Fr(1,2),)):
            # position of M
            if p==0: M=u[0]-1
            elif p==m: M=u[m-1]+1
            else: M=u[p-1]+(u[p]-u[p-1])*th
            # vars: g_0..g_{m-1}, tL, tR, d
            N=m+3; iL=m; iR=m+1; iD=m+2
            A=[];b=[]
            def row(): return [Fr(0)]*N
            for i in range(m):
                r=row(); r[i]=1; r[iD]=-1; A.append(r); b.append(cum[i])
                r=row(); r[i]=-1; r[iD]=-1; A.append(r); b.append(-cum[i+1])
            for i in range(1,m):
                r=row(); r[i-1]=1; r[i]=-1; A.append(r); b.append(0)
            r=row(); r[m-1]=1; A.append(r); b.append(1)
            cm1 = cum[p]   # F just left of M... Fn(M)=cum[p]
            # tL,tR bands & order
            for t in (iL,iR):
                r=row(); r[t]=1; r[iD]=-1; A.append(r); b.append(cm1)
                r=row(); r[t]=-1; r[iD]=-1; A.append(r); b.append(-cm1)
            r=row(); r[iL]=1; r[iR]=-1; A.append(r); b.append(0)
            if p>0:
                r=row(); r[p-1]=1; r[iL]=-1; A.append(r); b.append(0)
            if p<m:
                r=row(); r[iR]=1; r[p]=-1; A.append(r); b.append(0)
            # convex chain: points 0..p-1 then (M,tL)
            chain=[(u[i],i) for i in range(p)]+[(M,iL)]
            for k in range(1,len(chain)-1):
                (x0,v0),(x1,v1),(x2,v2)=chain[k-1],chain[k],chain[k+1]
                a=x1-x0; bb=x2-x1
                r=row(); r[v1]+=bb+a; r[v0]-=bb; r[v2]-=a; A.append(r); b.append(0)
            chain=[(M,iR)]+[(u[i],i) for i in range(p,m)]
            for k in range(1,len(chain)-1):
                (x0,v0),(x1,v1),(x2,v2)=chain[k-1],chain[k],chain[k+1]
                a=x1-x0; bb=x2-x1
                r=row(); r[v1]-=bb+a; r[v0]+=bb; r[v2]+=a; A.append(r); b.append(0)
            cvec=[0]*(N-1)+[1]
            res=simplex_min(cvec,A,b)
            if res is None: continue
            if best is None or res[0]<best: best=res[0]
    return best


if __name__ == "__main__":
    import sys
    print(dip_lp2([int(a) for a in sys.argv[1:]]))
