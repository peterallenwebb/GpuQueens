
struct queenInit
{
	char blah; // TODO
};

struct queenResult
{
	long solutions;
};

enum Step { Place, Remove };

typedef long qint;

__constant const int q = 15;

__constant const qint dodge = (1 << q) - 1;

__kernel void place(__global struct queenInit * init, 
                    __global struct queenResult * results)
{
  int index = get_global_id(0);

  long n = 0;
  qint masks[q];
  int col = 0;
  qint rook = 0;
  qint add = 0;
  qint sub = 0;
  enum 
  Step step = Place;

  qint mask = dodge & ~(rook | (add >> col) | (sub >> ((q - 1) - col)));
  qint rext = 0;

  if (mask == 0)
  	step = Remove;

while (true)
{
  if (step == Remove)
  {
    if (col == 0)
    {
      results[index].solutions = n;
      return;
    }

    --col;
    mask = masks[col];
  }

  rext = mask & -mask;
  rook ^= rext;
  add  ^= rext << col;
  sub  ^= rext << (q - 1 - col);

  if (step == Place)
  {
    masks[col] = mask;
    ++col;

    if (col != q)
    {
      mask = dodge & ~(rook | (add >> col) | (sub >> ((q - 1) - col)));

      if (mask == 0)
        step = Remove;
    }
    else
    {
      n += 1;
      step = Remove;
    }
  }
  else
  {
    mask ^= rext;

    if (mask == 0)
      step = Remove;
    else
      step = Place; 
  }
}
}
