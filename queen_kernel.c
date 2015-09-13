#define GCC_STYLE

#ifdef GCC_STYLE
  #include "stdio.h"
  #include "stdint.h"
  typedef int64_t qint;
  int get_global_id(int dimension) { return 0; }
  #define CL_KERNEL_KEYWORD
  #define CL_GLOBAL_KEYWORD
  #define CL_CONSTANT_KEYWORD
  #define CL_PACKED_KEYWORD
  #define NUM_QUEENS 14
#else
  typedef long qint;
  typedef long int64_t;
  #define CL_KERNEL_KEYWORD __kernel
  #define CL_GLOBAL_KEYWORD __global
  #define CL_CONSTANT_KEYWORD __constant
  #define CL_PACKED_KEYWORD  __attribute__ ((packed))
#endif

CL_CONSTANT_KEYWORD const int q = NUM_QUEENS;

#define PLACE 0
#define REMOVE 1
#define DONE 2

// State of individual computation
struct CL_PACKED_KEYWORD queenState
{
  qint masks[q];
  int64_t solutions; // Number of solutinos found so far.
  char step;
  char col;
  char startCol; // First column in which this individual computation was tasked with filling.
  qint mask;
  qint rext;
  qint rook;
  qint add;
  qint sub;
};

CL_CONSTANT_KEYWORD const qint dodge = (1 << q) - 1;

CL_KERNEL_KEYWORD void place(CL_GLOBAL_KEYWORD struct queenState * state)
{
  int index = get_global_id(0);

  int step     = state[index].step;
  int col      = state[index].col;
  int startCol = state[index].startCol;
  qint mask    = state[index].mask;
  qint rook    = state[index].rook;
  qint add     = state[index].add;
  qint sub     = state[index].sub;
  int64_t solutions = state[index].solutions;

  qint masks[q];
  for (int i = 0; i < q; i++)
    masks[i] = state[index].masks[i];

  while (1)
  {
    qint rext;

    if (step == REMOVE)
    {
      if (col == startCol)
      {
      	state[index].solutions = solutions;
        step = DONE;
       
#ifdef GCC_STYLE
        printf("%llu\n", solutions);
        return;
#else
        // TODO 
        // TODO 
        // TODO // TODO 
        // TODO 
        // TODO 
        // TODO // TODO 
        break;
#endif

      }

      --col;
      mask = masks[col];
    }

    rext = mask & -mask;
    rook ^= rext;
    add  ^= rext << col;
    sub  ^= rext << (q - 1 - col);

    if (step == PLACE)
    {
      masks[col] = mask;
      ++col;

      if (col != q)
      {
        mask = dodge & ~(rook | (add >> col) | (sub >> ((q - 1) - col)));

        if (mask == 0)
          step = REMOVE;
      }
      else
      {
        solutions += 1;
        step = REMOVE;
      }
    }
    else
    {
      mask ^= rext;

      if (mask == 0)
        step = REMOVE;
      else
        step = PLACE;
    }
  }

  // TODO: SAVE STATE
}

#ifdef GCC_STYLE

int main()
{
    struct queenState state = { };
    state.mask = dodge;
    place(&state);
}

#endif
