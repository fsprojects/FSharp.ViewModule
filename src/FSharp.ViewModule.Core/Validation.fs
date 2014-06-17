﻿(*
Copyright (c) 2013-2014 FSharp.ViewModule Team

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*)

namespace FSharp.ViewModule.Core.Validation

open System

type ValidationResult<'a> =
| Valid of name : string * value : 'a
| InvalidCollecting of name : string * value : 'a * error : string list
| InvalidDone of name : string * value : 'a * error : string list

[<AutoOpen>]
module Validators =
    let private createValidator predicate (error : string) (step : ValidationResult<'a>) =        
        let success = 
            match step with            
            | InvalidDone(_, _, _) -> false // Short circuit
            | InvalidCollecting(_, value, _) -> predicate value
            | Valid(_, value) -> predicate value
        
        match success, step with
        | _, InvalidDone(_, _, _) -> step // If our errors are fixed coming in, just pass through
        | true, Valid(name, value) -> Valid(name, value)
        | true,  InvalidCollecting(name, value, err) -> InvalidCollecting(name, value, err)
        | false, Valid(name, value) -> InvalidCollecting(name, value, [String.Format(error, name, value)])
        | false, InvalidCollecting(name, value, err) -> InvalidCollecting(name, value, err @ [String.Format(error, name, value)])

    /// Create a custom validator using a predicate ('a -> bool) and an error message on failure. The error message can use {0} for a placeholder for the property name.
    let custom predicate (error : string) (step : ValidationResult<'a>) = createValidator

    /// Fix the current state of errors, bypassing all future validation checks if we're in an error state
    let fixErrors (step : ValidationResult<'a>) =
        match step with
        | InvalidCollecting(name, value, errors) -> 
            // Switch us from invalid to invalid with errors fixed
            InvalidDone(name, value, errors)
        | _ -> step

    /// Begin a validation chain for a given property name
    let validate propertyName value = Valid(propertyName, value)
    
    // String validations
    let notNullOrWhitespace (str : ValidationResult<string>) = 
        let validation value = not(String.IsNullOrWhiteSpace(value))
        createValidator validation "{0} cannot be null or empty." str 

    let noSpaces (str : ValidationResult<string>) = 
        let validation (value : string) = not(value.Contains(" "))
        createValidator validation "{0} cannot contain a space." str

    let hasLength (length : int) (str : ValidationResult<string>) = 
        let validation (value : string) = value.Length = length
        createValidator validation ("{0} must be " + length.ToString() + " characters long.") str

    let hasLengthAtLeast (length : int) (str : ValidationResult<string>) = 
        let validation (value : string) = value.Length >= length
        createValidator validation ("{0} must be at least " + length.ToString() + " characters long.") str

    let hasLengthNoLongerThan (length : int) (str : ValidationResult<string>) = 
        let validation (value : string) = value.Length <= length
        createValidator validation ("{0} must be no longer than " + length.ToString() + " characters long.") str

    // Generic validations
    let notEqual value step = 
        createValidator (fun v -> value <> v) "{0} cannot equal {1}." step

    let greater value step =
        createValidator (fun v -> v > value) "{0} must be greater than {1}." step

    let greaterOrEqual value step =
        createValidator (fun v -> v >= value) "{0} must be greater than or equal to {1}." step

    let lessOrEqual value step =
        createValidator (fun v -> v < value) "{0} must be less than or equal to {1}." step
    
    let result (step : ValidationResult<'a>) : string list =
        match step with
        | Valid(_, _) -> []
        | InvalidCollecting(_, _, err) -> err
        | InvalidDone(_, _, err) -> err

    /// Produces a result of the validation, using a custom error message if an error occurred
    let resultWithError customErrorMessage (step : ValidationResult<'a>) : string list =
        match step with
        | Valid(_, value) -> []
        | _ -> [customErrorMessage]

    let evaluate value (workflow : 'a -> string list) =
        workflow(value)

