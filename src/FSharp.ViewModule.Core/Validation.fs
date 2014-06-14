(*
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

type ValidationStep<'a> =
| Valid of name : string * value : 'a
| Invalid of name : string * error : string

[<AutoOpen>]
module Validators =
    let createValidator (step : ValidationStep<'a>) predicate (error : string) =
        match step with
        | Invalid(name, err) -> Invalid(name, err)
        | Valid(name, x) ->
            if predicate x then Valid(name, x)
            else Invalid(name, String.Format(error, name, x))

    let validate name value = Valid(name, value)
    
    // String validations
    let notNullOrWhitespace (str : ValidationStep<string>) = 
        let validation value = not(String.IsNullOrWhiteSpace(value))
        createValidator str validation "{0} cannot be null or empty"

    let noSpaces (str : ValidationStep<string>) = 
        let validation (value : string) = not(value.Contains(" "))
        createValidator str validation "{0} cannot contain a space"

    let hasLength (length : int) (str : ValidationStep<string>) = 
        let validation (value : string) = value.Length = length
        createValidator str validation ("{0} must be " + length.ToString() + " characters long")

    let hasLengthAtLeast (length : int) (str : ValidationStep<string>) = 
        let validation (value : string) = value.Length >= length
        createValidator str validation ("{0} must be at least " + length.ToString() + " characters long")

    let hasLengthNoLongerThan (length : int) (str : ValidationStep<string>) = 
        let validation (value : string) = value.Length <= length
        createValidator str validation ("{0} must be no longer than " + length.ToString() + " characters long")

    // Generic validations
    let notEqual value step = 
        createValidator step (fun v -> value <> v) "{0} cannot equal {1}"

    let greater value step =
        createValidator step (fun v -> v > value) "{0} must be greater than {1}"

    let greaterOrEqual value step =
        createValidator step (fun v -> v >= value) "{0} must be greater than or equal to {1}"

    let lessOrEqual value step =
        createValidator step (fun v -> v < value) "{0} must be less than or equal to {1}"
    
    let result (step : ValidationStep<'a>) : Option<string> =
        match step with
        | Valid(_, value) -> None
        | Invalid(_, err) -> Some err

