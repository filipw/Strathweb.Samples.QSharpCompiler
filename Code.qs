namespace HelloQuantum {

    open Microsoft.Quantum.Canon;
    open Microsoft.Quantum.Measurement;
    open Microsoft.Quantum.Intrinsic;
    open Microsoft.Quantum.Convert;

    @EntryPoint()
    operation HelloQ() : Unit {
        let ones = GetRandomBit(100);
        Message("Ones: " + IntAsString(ones));
        Message("Zeros: " + IntAsString((100 - ones)));
    }
	
    operation GetRandomBit(count : Int) : Int {
 
        mutable resultsTotal = 0;
 
        use qubit = Qubit();       
            for idx in 0..count {               
                H(qubit);
                let result = MResetZ(qubit);
                set resultsTotal += result == One ? 1 | 0;
            }
            return resultsTotal;
    }
}