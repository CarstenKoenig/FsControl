namespace FsControl.Core.TypeMethods

open System
open System.Text
open Microsoft.FSharp.Quotations
open FsControl.Core.Prelude
open FsControl.Core.Types
open Monoid

type internal keyValue<'a,'b> = System.Collections.Generic.KeyValuePair<'a,'b>

// Monad class ------------------------------------------------------------
module Monad =

    type Bind = Bind with
        static member        instance (Bind, x:option<_>    , _:option<'b>   ) = fun (f:_->option<'b>) -> Option.bind   f x
        static member        instance (Bind, x:List<_>      , _:List<'b>     ) = fun (f:_->List<'b>  ) -> List.collect  f x
        static member        instance (Bind, x:_ []         , _:'b []        ) = fun (f:_->'b []     ) -> Array.collect f x
        static member        instance (Bind, f:'r->'a       , _:'r->'b       ) = fun (k:_->_->'b) r    -> k (f r) r
        static member inline instance (Bind, (w, a):'m * 'a , _:'m * 'b      ) = fun (k:_->'m * 'b   ) -> let m, b = k a in (mappend w m, b)
        static member        instance (Bind, x:Async<'a>    , _:'b Async     ) = fun (f:_->Async<'b> ) -> async.Bind(x,f)
        static member        instance (Bind, x:Choice<'a,'e>, _:Choice<'b,'e>) = fun (k:'a->Choice<'b,'e>) -> Error.bind k x
        static member        instance (Bind, x:Map<'k,'a>   , _:Map<'k,'b>   ) = fun (f:'a->Map<'k,'b>) -> Map (seq {
            for KeyValue(k, v) in x do
                match Map.tryFind k (f v) with
                | Some v -> yield k, v
                | _      -> () })

        //Restricted Monad
        static member instance (Bind, x:Nullable<_> , _:'b Nullable) = fun f -> if x.HasValue then f x.Value else Nullable() : Nullable<'b>

    let inline internal (>>=) x (f:_->'R) : 'R = Inline.instance (Bind, x) f


    type Join = Join with
        static member        instance (Join, x:option<option<'a>>    , _ , _:option<'a>   ) = fun () -> Option.bind   id x
        //static member        instance (Join, x:List<_>      , _ , _:List<'b>     ) = fun () -> List.collect  id x
        static member        instance (Join, x:'b [] []         , _ , _:'b []        ) = fun () -> Array.collect id x

        // Default Implementation
        static member inline instance (Join, _:obj , x:#obj, _:#obj) = fun () -> x >>= id :#obj

    let inline internal join (x:'Monad'Monad'a) : 'Monad'a = Inline.instance (Join, x, x) ()

open Monad

module Applicative =
    type Pure = Pure with
        static member        instance (Pure, _:option<'a>    ) = fun x -> Some x      :option<'a>
        static member        instance (Pure, _:List<'a>      ) = fun x -> [ x ]       :List<'a>
        static member        instance (Pure, _:'a []         ) = fun x -> [|x|]       :'a []
        static member        instance (Pure, _:'r -> 'a      ) = const':'a  -> 'r -> _
        static member inline instance (Pure, _: 'm * 'a      ) = fun (x:'a) -> (mempty(), x)
        static member        instance (Pure, _:'a Async      ) = fun (x:'a) -> async.Return x
        static member        instance (Pure, _:Choice<'a,'e> ) = fun x -> Choice1Of2 x :Choice<'a,'e>
        static member        instance (Pure, _:Expr<'a>      ) = fun x -> <@ x @>     :Expr<'a>
        static member        instance (Pure, _:'a ResizeArray) = fun x -> new ResizeArray<'a>(Seq.singleton x)        

        //Restricted
        static member instance (Pure, _:'a Nullable  ) = fun (x:'a  ) -> Nullable x:'a Nullable
        static member instance (Pure, _:string       ) = fun (x:char) -> string x : string
        static member instance (Pure, _:StringBuilder) = fun (x:char) -> new StringBuilder(string x):StringBuilder
        static member instance (Pure, _:'a Set       ) = fun (x:'a  ) -> Set.singleton x
        

    let inline internal pure' x   = Inline.instance Pure x


    type DefaultImpl =        
        static member inline ApplyFromMonad f x = f >>= fun x1 -> x >>= fun x2 -> pure'(x1 x2)

    type Apply = Apply with
        // static member        instance (Apply, f:List<_>     , x:List<'a>  , _, _:List<'b>     ) = fun () -> DefaultImpl.ApplyFromMonad f x :List<'b>
        // static member        instance (Apply, f:_ []        , x:'a []     , _, _:'b []        ) = fun () -> DefaultImpl.ApplyFromMonad f x :'b []
        static member        instance (Apply, f:'r -> _     , g: _ -> 'a  , _, _: 'r -> 'b    ) = fun () -> fun x -> f x (g x) :'b
        static member inline instance (Apply, (a:'m, f)     , (b:'m, x:'a), _, _:'m * 'b      ) = fun () -> (mappend a b, f x) :'m *'b
        // static member        instance (Apply, f:Async<_>    , x:Async<'a> , _, _:Async<'b>    ) = fun () -> DefaultImpl.ApplyFromMonad f x :Async<'b>

        // static member        instance (Apply, f:option<_>   , x:option<'a>, _, _:option<'b>   ) = fun () -> 
        //     match (f,x) with 
        //     | Some f, Some x -> Some (f x) 
        //     | _              -> None :option<'b>
        // 
        // static member        instance (Apply, f:Choice<_,'e>, x:Choice<'a,'e>, _, _:Choice<'b,'e>) = fun () ->
        //     match (f,x) with
        //     | (Choice1Of2 a, Choice1Of2 b) -> Choice1Of2 (a b)
        //     | (Choice2Of2 a, _)            -> Choice2Of2 a
        //     | (_, Choice2Of2 b)            -> Choice2Of2 b :Choice<'b,'e>

        static member        instance (Apply, KeyValue(k:'k,f), KeyValue(k:'k,x:'a), _, _:keyValue<'k,'b>) :unit->keyValue<'k,'b> = fun () -> keyValue(k, f x)
        static member        instance (Apply, f:Map<'k,_>     , x:Map<'k,'a>       , _, _:Map<'k,'b>     ) :unit->Map<'k,'b>      = fun () -> Map (seq {
            for KeyValue(k, vf) in f do
                match Map.tryFind k x with
                | Some vx -> yield k, vf vx
                | _       -> () })

        static member        instance (Apply, f:_ Expr      , x:'a Expr   , _, _:'b Expr      ) = fun () -> <@ (%f) %x @> :'b Expr

        static member        instance (Apply, f:('a->'b) ResizeArray, x:'a ResizeArray, _, _:'b ResizeArray) = fun () ->
            new ResizeArray<'b>(Seq.collect (fun x1 -> Seq.collect (fun x2 -> Seq.singleton (x1 x2)) x) f) :'b ResizeArray

        // Default Implementation
        static member inline instance (Apply, _:obj , x, f:#obj, _:#obj) = fun () -> (f >>= fun x1 -> x >>= fun x2 -> pure'(x1 x2)) :#obj

        
   
    let inline internal (<*>) x y = Inline.instance (Apply, x, y, x) ()

open Applicative


module Alternative =
    type Empty = Empty with
        static member instance (Empty, _:option<'a>) = fun () -> None
        static member instance (Empty, _:List<'a>  ) = fun () -> [  ]
        static member instance (Empty, _:'a []     ) = fun () -> [||]

    type Append = Append with  
        static member instance (Append, x:option<_>, _) = fun y -> match x with None -> y | xs -> xs
        static member instance (Append, x:List<_>  , _) = fun y -> x @ y
        static member instance (Append, x:_ []     , _) = fun y -> Array.append x y


// Functor class ----------------------------------------------------------

module Functor =
    type Map = Map with
        static member        instance (Map, x:option<_>    , _ , _) = fun f -> Option.map  f x
        static member        instance (Map, x:List<_>      , _ , _:List<'b>) = fun f -> List.map f x :List<'b>
        static member        instance (Map, g:_->_         , _ , _) = (>>) g
        static member        instance (Map, (m,a)          , _ , _) = fun f -> (m, f a)
        static member        instance (Map, x:_ []         , _ , _) = fun f -> Array.map   f x
        static member        instance (Map, x:_ [,]        , _ , _) = fun f -> Array2D.map f x
        static member        instance (Map, x:_ [,,]       , _ , _) = fun f -> Array3D.map f x
        static member        instance (Map, x:_ [,,,]      , _ , _) = fun f ->
            Array4D.init (x.GetLength 0) (x.GetLength 1) (x.GetLength 2) (x.GetLength 3) (fun a b c d -> f x.[a,b,c,d])
        // static member        instance (Map, x:Async<_>      , _, _) = fun f -> DefaultImpl.MapFromMonad f x
        static member        instance (Map, x:Choice<_,_>   , _, _) = fun f -> Error.map f x
        static member        instance (Map, KeyValue(k, x)  , _, _) = fun (f:'b->'c) -> keyValue(k, f x)
        static member        instance (Map, x:Map<'a,'b>    , _, _) = fun (f:'b->'c) -> Map.map (const' f) x : Map<'a,'c>
        static member        instance (Map, x:Expr<_>       , _, _) = fun f -> <@ f %x @>
        static member        instance (Map, x:_ ResizeArray , _, _) = fun f -> new ResizeArray<'b>(Seq.map f x)
        static member        instance (Map, x:_ IObservable , _, _) = fun f -> Observable.map f x

        // Default Implementation
        static member inline instance (Map, _:obj , x:#obj, _:#obj) = fun (f:'a->'b) -> pure' f <*> x :#obj

        // Restricted
        static member        instance (Map, x:Nullable<_>  , _, _) = fun f -> if x.HasValue then Nullable(f x.Value) else Nullable()
        static member        instance (Map, x:string       , _, _) = fun f -> String.map f x
        static member        instance (Map, x:StringBuilder, _, _) = fun f -> new StringBuilder(String.map f (x.ToString()))
        static member        instance (Map, x:Set<_>       , _, _) = fun f -> Set.map f x
        

    let inline internal fmap   f x = Inline.instance (Map, x, x) f

   
    let inline internal sequence ms =
        let k m m' = m >>= fun (x:'a) -> m' >>= fun xs -> (pure' :list<'a> -> 'M) (List.Cons(x,xs))
        List.foldBack k ms ((pure' :list<'a> -> 'M) [])

    let inline internal mapM f as' = sequence (List.map f as')

    let inline internal liftM  f m1    = m1 >>= (pure' << f)
    let inline internal liftM2 f m1 m2 = m1 >>= fun x1 -> m2 >>= fun x2 -> pure' (f x1 x2)
    let inline internal ap     x y     = liftM2 id x y

    let inline internal (>=>)  f g x   = f x >>= g

    // Do notation ------------------------------------------------------------

    type DoNotationBuilder() =
        member inline b.Return(x)    = pure' x
        member inline b.Bind(p,rest) = p >>= rest
        member        b.Let (p,rest) = rest p
        member    b.ReturnFrom(expr) = expr
    let inline  internal do'() = new DoNotationBuilder()

    let inline internal return' x = Inline.instance Pure x


open Functor

module Comonad =

    type Extract = Extract with
        static member        instance (Extract, (w:'w,a:'a) ,_) = fun () -> a
        static member inline instance (Extract, f:'m->'t ,_:'t) = fun () -> f (mempty())

        // Restricted
        static member        instance (Extract, x:'t list         , _:'t) = fun () -> List.head x
        static member        instance (Extract, x:'t []           , _:'t) = fun () -> x.[0]
        static member        instance (Extract, x:string          , _   ) = fun () -> x.[0]
        static member        instance (Extract, x:StringBuilder   , _   ) = fun () -> x.ToString().[0]

    let inline internal extract x = Inline.instance (Extract, x) ()


    type Duplicate = Duplicate with
        static member        instance (Duplicate, (w:'w, a:'a), _:'w * ('w*'a)) = fun () -> (w,(w,a))
        static member inline instance (Duplicate, f:'m->'a, _:'m->'m->'a) = fun () a b -> f (mappend a b)

        // Restricted
        static member        instance (Duplicate, s:List<'a>, _:List<List<'a>>) = fun () -> 
            let rec tails = function [] -> [] | x::xs as s -> s::(tails xs)
            tails s

    let inline internal duplicate x = Inline.instance (Duplicate, x) ()


    let inline internal extend g s = fmap g (duplicate s)
    let inline internal (=>>)  s g = fmap g (duplicate s)


// MonadPlus class ------------------------------------------------------------
module MonadPlus =
    type Mzero = Mzero with
        static member instance (Mzero, _:option<'a>) = fun () -> None
        static member instance (Mzero, _:List<'a>  ) = fun () -> [  ]
        static member instance (Mzero, _:'a []     ) = fun () -> [||]

    type Mplus = Mplus with
        static member instance (Mplus, x:option<_>, _) = fun y -> match x with None -> y | xs -> xs
        static member instance (Mplus, x:List<_>  , _) = fun y -> x @ y
        static member instance (Mplus, x:_ []     , _) = fun y -> Array.append x y

    let inline internal mzero () = Inline.instance Mzero ()
    let inline internal mplus (x:'a) (y:'a) : 'a = Inline.instance (Mplus, x) y
