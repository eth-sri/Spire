#!/bin/bash

SPIRE_PATH="./Spire/bin/x64/Release/Permissive policy synthesis.exe"

mkdir iterate_location
cd iterate_location
cp ./../location/location-identity_12_1_policy.psi .
cp ./../location/location-identity_12_1_prior.psi .
cp ./../location/location-identity_12_1_program.psi .
mono "./../${SPIRE_PATH}" --psi-path="" --prior="location-identity_12_1_prior.psi" --program="location-identity_12_1_program.psi" --policy="location-identity_12_1_policy.psi" --tmp-prefix="_" --log="log.log" --csv="log.csv" --opt-goal="classes" --smt-lib-log="formulas.smtlib2" --iterations=20 --input="0,0" --iteration-log="iter_log.csv" --iterate-heur
