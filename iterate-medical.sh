#!/bin/bash

SPIRE_PATH="./Spire/bin/x64/Release/Permissive policy synthesis.exe"

mkdir iterate_medical
cd iterate_medical
cp ./../medical/medical-noise_6_1_policy.psi .
cp ./../medical/medical-noise_6_1_prior.psi .
cp ./../medical/medical-noise_6_1_program.psi .
mono "./../${SPIRE_PATH}" --psi-path="" --prior="medical-noise_6_1_prior.psi" --program="medical-noise_6_1_program.psi" --policy="medical-noise_6_1_policy.psi" --tmp-prefix="_" --log="log.log" --csv="log.csv" --opt-goal="classes" --smt-lib-log="formulas.smtlib2" --iterations=20 --input="0,1,0,1,0,1,0,1,0,1,0,1" --iteration-log="iter_log.csv" --iterate-heur
