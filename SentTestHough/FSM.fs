//mechanim for defining and evaluating "functional state machines"
module FSM

type F<'State,'OutEvent> = F of ('State -> F<'State,'OutEvent>)*'OutEvent option

let evalState (F(state,_)) event = state event

let evalStates states event =
    let states,e = (([],None),states) ||> List.fold (fun (acc,re) f -> 
        match re with
        | Some e -> f::acc,Some e
        | None   -> 
            let (F (state,_)) = f
            match state event with
            | F(nextState, Some e) -> (F(nextState,None))::acc,Some e
            | F(nextState, None)   -> (F(nextState,None))::acc,None
        )
    (List.rev states),e
